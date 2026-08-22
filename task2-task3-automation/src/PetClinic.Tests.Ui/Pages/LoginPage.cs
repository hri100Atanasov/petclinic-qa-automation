using Microsoft.Playwright;

namespace PetClinic.Tests.Ui.Pages;

public class LoginPage(IPage page)
{
    private ILocator UsernameInput => page.GetByTestId("login-username");
    private ILocator PasswordInput => page.GetByTestId("login-password");
    private ILocator SubmitButton => page.GetByTestId("login-submit");
    public ILocator ErrorMessage => page.GetByTestId("login-error");
    public ILocator SignOutButton => page.GetByTestId("logout-button");

    public Task NavigateAsync(string baseUrl) => page.GotoAsync(baseUrl);

    public async Task LoginAsync(string username, string password)
    {
        await UsernameInput.FillAsync(username);
        await PasswordInput.FillAsync(password);
        await SubmitButton.ClickAsync();

        // Neutral wait, not an assertion: the login POST + any resulting redirect
        // need to settle before a caller navigates elsewhere, or that navigation
        // can race the login and land unauthenticated. Whether login actually
        // succeeded is still for the caller to assert.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
