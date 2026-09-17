using Microsoft.AspNetCore.Components.Authorization;

namespace MoneyBack.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokenStore;

    public CustomAuthStateProvider(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _tokenStore.OnChange += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_tokenStore.CurrentPrincipal));
    }
}
