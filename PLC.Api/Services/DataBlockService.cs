    using PLC.Api.DTOs.DataBlock;
using PLC.Api.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace PLC.Api.Services
{
    public class DataBlockService : IDataBlockService
    {
        #region Parse

        public (string Name, List<DataBlockVariable> Variables) Parse(Stream file)
        {
            using StreamReader reader = new(file);
            string content = reader.ReadToEnd();

            string name = ParseDataBlockName(content);
            List<string> lines = GetRelevantLines(content);

            int currentLine = 0;
            List<DataBlockVariable> variables = new();

            ParseRootVariables(
                lines,
                ref currentLine,
                variables);

            CalculateOffsets(variables);

            return (name, variables);
        }

        private string ParseDataBlockName(string content)
        {
            Match match = Regex.Match(
                content,
                @"DATA_BLOCK\s+""([^""]+)""",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                throw new FormatException(
                    "Data Block adı bulunamadı.");

            return match.Groups[1].Value;
        }

        private List<string> GetRelevantLines(string content)
        {
            string[] lines = content.Split(
                new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.None);

            int startIndex = -1;
            bool isVarBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Equals("VAR", StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = i + 1;
                    isVarBlock = true;
                    break;
                }

                if (line.Equals("STRUCT", StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = i + 1;
                    isVarBlock = false;
                    break;
                }
            }

            if (startIndex < 0)
                throw new FormatException(
                    "Data Block içerisinde VAR veya STRUCT yapısı bulunamadı.");

            List<string> result = new();

            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (isVarBlock &&
                    line.Equals("END_VAR", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!isVarBlock &&
                    line.Equals("END_STRUCT", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                result.Add(line);
            }

            return result;
        }

        private void ParseRootVariables(
            List<string> lines,
            ref int currentLine,
            List<DataBlockVariable> variables)
        {
            ParseVariables(
                lines,
                ref currentLine,
                variables,
                null);
        }

        private void ParseVariables(
            List<string> lines,
            ref int currentLine,
            List<DataBlockVariable> variables,
            DataBlockVariable? parent)
        {
            while (currentLine < lines.Count)
            {
                string line = lines[currentLine].Trim();

                if (line.Equals(
                        "END_STRUCT;",
                        StringComparison.OrdinalIgnoreCase) ||
                    line.Equals(
                        "END_STRUCT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentLine++;
                    return;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    currentLine++;
                    continue;
                }

                Match variableMatch = Regex.Match(
                    line,
                    @"^(?:""(?<quotedName>[^""]+)""|(?<name>[A-Za-z_][A-Za-z0-9_]*))\s*:\s*(?<type>.+?);?$",
                    RegexOptions.IgnoreCase);

                if (!variableMatch.Success)
                {
                    currentLine++;
                    continue;
                }

                string variableName =
                    variableMatch.Groups["quotedName"].Success
                        ? variableMatch.Groups["quotedName"].Value
                        : variableMatch.Groups["name"].Value;

                string typeDefinition =
                    variableMatch.Groups["type"].Value.Trim();

                string path = BuildPath(parent, variableName);

                DataBlockVariable variable = new()
                {
                    Name = variableName,
                    Path = path,
                    TypeDefinition = typeDefinition,
                    Parent = parent
                };

                variables.Add(variable);

                currentLine++;

                if (IsStructType(typeDefinition))
                {
                    ParseStructChildren(
                        lines,
                        ref currentLine,
                        variables,
                        variable);

                    continue;
                }

                if (IsArrayOfStruct(typeDefinition))
                {
                    ParseArrayStructChildren(
                        lines,
                        ref currentLine,
                        variables,
                        variable);

                    continue;
                }
            }
        }

        private void ParseStructChildren(
            List<string> lines,
            ref int currentLine,
            List<DataBlockVariable> variables,
            DataBlockVariable parent)
        {
            ParseVariables(
                lines,
                ref currentLine,
                variables,
                parent);
        }

        private void ParseArrayStructChildren(
            List<string> lines,
            ref int currentLine,
            List<DataBlockVariable> variables,
            DataBlockVariable parent)
        {
            ParseVariables(
                lines,
                ref currentLine,
                variables,
                parent);
        }

        private string BuildPath(
            DataBlockVariable? parent,
            string variableName)
        {
            string segment = $"\"{variableName}\"";

            return parent == null
                ? segment
                : $"{parent.Path}.{segment}";
        }

        #endregion

        #region Type Detection

        private bool IsStructType(string typeDefinition)
        {
            return typeDefinition.Equals(
                "Struct",
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsArrayOfStruct(string typeDefinition)
        {
            if (!typeDefinition.StartsWith(
                    "Array",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string elementType =
                GetArrayElementType(typeDefinition);

            return elementType.Equals(
                "Struct",
                StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Offset Calculation

        private void CalculateOffsets(
            List<DataBlockVariable> variables)
        {
            List<DataBlockVariable> rootVariables =
                variables
                    .Where(v => v.Parent == null)
                    .ToList();

            OffsetState state = new();

            foreach (DataBlockVariable variable in rootVariables)
            {
                CalculateVariableOffset(
                    variable,
                    state,
                    variables);
            }
        }

        private void CalculateVariableOffset(
            DataBlockVariable variable,
            OffsetState state,
            List<DataBlockVariable> allVariables)
        {
            string type =
                variable.TypeDefinition.Trim();

            if (IsBool(type))
            {
                variable.ByteOffset = state.Byte;
                variable.BitOffset = state.Bit;
                variable.Size = 1;

                state.Bit++;

                if (state.Bit >= 8)
                {
                    state.Bit = 0;
                    state.Byte++;
                }

                return;
            }

            AlignToByte(state);

            if (IsStructType(type) ||
                IsArrayOfStruct(type))
            {
                int startByte = state.Byte;

                variable.ByteOffset = startByte;
                variable.BitOffset = 0;

                OffsetState childState = new()
                {
                    Byte = startByte,
                    Bit = 0
                };

                List<DataBlockVariable> children =
                    allVariables
                        .Where(v => v.Parent == variable)
                        .ToList();

                foreach (DataBlockVariable child in children)
                {
                    CalculateVariableOffset(
                        child,
                        childState,
                        allVariables);
                }

                AlignToByte(childState);

                int structSize =
                    childState.Byte - startByte;

                if (IsArrayOfStruct(type))
                {
                    int count =
                        GetArrayCount(type);

                    structSize *= count;
                }

                variable.Size = structSize;

                state.Byte += structSize;
                return;
            }

            variable.ByteOffset = state.Byte;
            variable.BitOffset = 0;
            variable.Size =
                GetDataTypeSize(type);

            state.Byte += variable.Size;
        }

        private void AlignToByte(OffsetState state)
        {
            if (state.Bit != 0)
            {
                state.Byte++;
                state.Bit = 0;
            }

            if (state.Byte % 2 != 0)
                state.Byte++;
        }

        private bool IsBool(string typeDefinition)
        {
            return typeDefinition.Equals(
                "Bool",
                StringComparison.OrdinalIgnoreCase);
        }

        private class OffsetState
        {
            public int Byte { get; set; }
            public byte Bit { get; set; }
        }

        #endregion

        #region Data Type Size

        public int GetDataTypeSize(
            string typeDefinition)
        {
            string type =
                typeDefinition.Trim();

            string upperType =
                type.ToUpperInvariant();

            if (upperType.StartsWith("STRING[") &&
                upperType.EndsWith("]"))
            {
                string lengthText =
                    upperType.Substring(
                        7,
                        upperType.Length - 8);

                if (!int.TryParse(
                        lengthText,
                        out int length))
                {
                    throw new FormatException(
                        $"STRING uzunluğu okunamadı: {typeDefinition}");
                }

                return length + 2;
            }

            if (upperType.StartsWith("ARRAY["))
            {
                string elementType =
                    GetArrayElementType(typeDefinition);

                int count =
                    GetArrayCount(typeDefinition);

                if (IsBool(elementType))
                    return (count + 7) / 8;

                if (IsStructType(elementType))
                    throw new InvalidOperationException(
                        "Array of Struct boyutu için struct içeriğinin boyutu gereklidir.");

                return count *
                       GetDataTypeSize(elementType);
            }

            return upperType switch
            {
                "BOOL" => 1,
                "BYTE" => 1,
                "WORD" => 2,
                "DWORD" => 4,
                "SINT" => 1,
                "USINT" => 1,
                "INT" => 2,
                "UINT" => 2,
                "DINT" => 4,
                "UDINT" => 4,
                "REAL" => 4,
                "LREAL" => 8,
                "TIME" => 4,
                "DATE" => 2,
                "TOD" => 4,
                "DT" => 8,
                "CHAR" => 1,
                "STRING" => 256,

                _ => throw new NotSupportedException(
                    $"Desteklenmeyen veri tipi: {typeDefinition}")
            };
        }

        #endregion

        #region Array Helpers

        public (int Lower, int Upper) GetArrayBounds(
            string typeDefinition)
        {
            Match match = Regex.Match(
                typeDefinition,
                @"ARRAY\s*\[\s*(-?\d+)\s*\.\.\s*(-?\d+)\s*\]",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new FormatException(
                    $"Array sınırları çözümlenemedi: {typeDefinition}");
            }

            int lower =
                int.Parse(match.Groups[1].Value);

            int upper =
                int.Parse(match.Groups[2].Value);

            if (upper < lower)
            {
                throw new FormatException(
                    $"Array sınırları geçersiz: {typeDefinition}");
            }

            return (lower, upper);
        }

        public int GetArrayCount(
            string typeDefinition)
        {
            var bounds =
                GetArrayBounds(typeDefinition);

            return bounds.Upper -
                   bounds.Lower +
                   1;
        }

        public string GetArrayElementType(
            string typeDefinition)
        {
            Match match = Regex.Match(
                typeDefinition,
                @"ARRAY\s*\[[^\]]+\]\s+OF\s+(.+)$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new FormatException(
                    $"Array eleman tipi çözümlenemedi: {typeDefinition}");
            }

            return match.Groups[1].Value.Trim();
        }

        #endregion

        #region Variable Navigation

        public Task<List<VariableChildResponse>> GetVariableChildrenAsync(
            DataBlockVariable variable,
            string path)
        {
            List<VariableChildResponse> result = new();

            string type = variable.TypeDefinition.Trim();

            // Array ise:
            // Eğer path henüz array instance'ı göstermiyorsa index'leri döndür.
            if (IsArrayType(type))
            {
                if (!PathEndsWithArrayIndex(path))
                {
                    var (lower, upper) =
                        GetArrayBounds(type);

                    for (int index = lower; index <= upper; index++)
                    {
                        string indexedPath =
                            $"{path}[{index}]";

                        result.Add(new VariableChildResponse
                        {
                            Index = index,
                            Name = $"{variable.Name}[{index}]",
                            Path = indexedPath,
                            HasChildren = IsArrayOfStruct(type)
                        });
                    }

                    return Task.FromResult(result);
                }
            }

            // Array instance'ına girilmişse veya normal Struct ise
            // template child'larını getir.
            List<DataBlockVariable> children =
                variable.Children;

            foreach (DataBlockVariable child in children)
            {
                string childPath =
                    $"{path}.\"{child.Name}\"";

                result.Add(new VariableChildResponse
                {
                    Id = child.Id,
                    Name = child.Name,
                    Path = childPath,
                    HasChildren =
                        IsArrayType(child.TypeDefinition.Trim()) ||
                        IsStructType(child.TypeDefinition.Trim())
                });
            }

            return Task.FromResult(result);
        }

        private bool IsArrayType(
            string typeDefinition)
        {
            return typeDefinition
                .Trim()
                .StartsWith(
                    "ARRAY[",
                    StringComparison.OrdinalIgnoreCase);
        }

        private bool PathEndsWithArrayIndex(
            string path)
        {
            return Regex.IsMatch(
                path.Trim(),
                @"\[\s*-?\d+\s*\]$");
        }

        #endregion
    }
}