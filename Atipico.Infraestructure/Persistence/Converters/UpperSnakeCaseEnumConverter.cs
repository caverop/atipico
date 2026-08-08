using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text;

namespace Atipico.Infraestructure.Persistence.Converters
{
    // Maps PascalCase enum members (EnPreparacion) to the UPPER_SNAKE_CASE
    // strings used by the varchar CHECK constraints (EN_PREPARACION).
    public class UpperSnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
        where TEnum : struct, Enum
    {
        public UpperSnakeCaseEnumConverter()
            : base(v => ToUpperSnakeCase(v.ToString()),
                   v => Enum.Parse<TEnum>(FromUpperSnakeCase(v)))
        {
        }

        private static string ToUpperSnakeCase(string value)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]))
                    sb.Append('_');
                sb.Append(value[i]);
            }
            return sb.ToString().ToUpperInvariant();
        }

        private static string FromUpperSnakeCase(string value)
        {
            var words = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var word in words)
                sb.Append(char.ToUpperInvariant(word[0])).Append(word[1..].ToLowerInvariant());
            return sb.ToString();
        }
    }
}
