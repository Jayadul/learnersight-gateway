namespace NetZeroGateway.API.Exceptions;
public class UnauthorizedException(string message = "Unauthorized.") : Exception(message) { }
