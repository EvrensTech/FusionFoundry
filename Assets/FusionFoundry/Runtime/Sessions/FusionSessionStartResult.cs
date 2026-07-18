using System;

namespace FusionFoundry.Sessions
{
    public sealed class FusionSessionStartResult
    {
        private FusionSessionStartResult(
            bool isSuccess,
            string userMessage,
            string diagnosticCode,
            string diagnosticMessage)
        {
            IsSuccess = isSuccess;
            UserMessage = userMessage;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
        }

        public bool IsSuccess { get; }

        public string UserMessage { get; }

        public string DiagnosticCode { get; }

        public string DiagnosticMessage { get; }

        public static FusionSessionStartResult Succeeded(string userMessage = "")
        {
            return new FusionSessionStartResult(
                true,
                userMessage ?? string.Empty,
                string.Empty,
                string.Empty);
        }

        public static FusionSessionStartResult Failed(
            string userMessage,
            string diagnosticCode = "",
            string diagnosticMessage = "")
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                throw new ArgumentException(
                    "A failed result must include a user-facing message.",
                    nameof(userMessage));
            }

            return new FusionSessionStartResult(
                false,
                userMessage,
                diagnosticCode ?? string.Empty,
                diagnosticMessage ?? string.Empty);
        }
    }
}
