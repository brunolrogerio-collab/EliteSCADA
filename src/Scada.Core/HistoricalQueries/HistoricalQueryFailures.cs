namespace Scada.Core.HistoricalQueries;

public sealed class HistoricalQueryValidationException(string message, Exception? innerException = null)
    : ArgumentException(message, innerException);

public sealed class HistoricalQueryProviderException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
