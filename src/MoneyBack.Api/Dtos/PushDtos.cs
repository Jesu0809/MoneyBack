namespace MoneyBack.Api.Dtos;

public record VapidPublicKeyResponse(string PublicKey);

public record SuscribirsePushRequest(string Endpoint, string P256dh, string Auth);
