namespace GestionPharmacie.BLL.Exceptions;

/// <summary>
/// Exception levée lors d'une violation de règle métier (ex: stock insuffisant, péremption dépassée, etc.).
/// </summary>
public class MetierException : Exception
{
    public MetierException(string message) : base(message) { }
    public MetierException(string message, Exception innerException) : base(message, innerException) { }
}
