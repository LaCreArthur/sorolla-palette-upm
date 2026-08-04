namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        public class ValidationResult
        {
            internal ReadinessCheck Check;
            public string Fix;
            public string Message;
            public ValidationStatus Status;

            internal ValidationResult(ValidationStatus status, string message, string fix, ReadinessCheck check)
            {
                Status = status;
                Message = message;
                Fix = fix;
                Check = check;
            }
        }
    }
}
