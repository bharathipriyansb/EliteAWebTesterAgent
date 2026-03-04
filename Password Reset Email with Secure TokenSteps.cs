using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

[Binding]
public class PasswordResetSteps
{
    private readonly ScenarioContext _scenarioContext;
    private IWebDriver Driver => _scenarioContext.ContainsKey("Driver") ? _scenarioContext["Driver"] as IWebDriver : null;

    // Test helpers that should be implemented/injected in your test framework
    private ITestSmtpClient _smtpClient => _scenarioContext.ContainsKey("SmtpClient") ? _scenarioContext["SmtpClient"] as ITestSmtpClient : null;
    private IServerApiClient _apiClient => _scenarioContext.ContainsKey("ApiClient") ? _scenarioContext["ApiClient"] as IServerApiClient : null;
    private TimeProvider _timeProvider => _scenarioContext.ContainsKey("TimeProvider") ? _scenarioContext["TimeProvider"] as TimeProvider : null;

    public PasswordResetSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    #region Background steps

    [Given(@"the password recovery system and SMTP service are operational")]
    public void GivenThePasswordRecoverySystemAndSmtpServiceAreOperational()
    {
        // Assume test harness verifies health endpoints; placeholder assertion:
        Assert.IsTrue(_apiClient?.IsHealthy() ?? true, "API health check failed or ApiClient not provided.");
        Assert.IsTrue(_smtpClient?.IsAvailable() ?? true, "SMTP test client not available.");
    }

    [Given(@"a registered user exists with email ""(.*)"" and known account details")]
    public void GivenARegisteredUserExistsWithEmailAndKnownAccountDetails(string email)
    {
        // Ensure test fixture has user; API client should create test user or verify existence
        var exists = _apiClient?.EnsureTestUserExists(email) ?? true;
        Assert.IsTrue(exists, $"Failed to ensure test user {email} exists.");
        _scenarioContext["TestUserEmail"] = email;
    }

    [Given(@"the test SMTP mailbox and server-side email logs are accessible to the test framework")]
    public void GivenTheTestSmtpMailboxAndServer_SideEmailLogsAreAccessibleToTheTestFramework()
    {
        Assert.IsNotNull(_smtpClient, "SMTP client not injected into ScenarioContext.");
        Assert.IsTrue(_smtpClient.CanQueryLogs(), "SMTP client cannot query logs.");
    }

    [Given(@"server time is synchronized with the test runner")]
    public void GivenServerTimeIsSynchronizedWithTheTestRunner()
    {
        // This should use a controllable TimeProvider in test envs. We assert presence.
        Assert.IsNotNull(_timeProvider, "TimeProvider not injected; time synchronization required for timing assertions.");
    }

    #endregion

    #region UI flow steps

    [Given(@"the user is on the login page")]
    public void GivenTheUserIsOnTheLoginPage()
    {
        Assert.IsNotNull(Driver, "WebDriver instance not provided in ScenarioContext under 'Driver'.");
        Driver.Navigate().GoToUrl(_apiClient?.GetAppUrl("/login") ?? "/login");
    }

    [Given(@"the user clicks the ""(.*)"" link")]
    public void GivenTheUserClicksTheLink(string linkText)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        var link = wait.Until(d => d.FindElement(By.LinkText(linkText)));
        link.Click();
    }

    [Given(@"the user is on the ""Forgot Password\?"" form")]
    public void GivenTheUserIsOnTheForgotPasswordForm()
    {
        GivenTheUserIsOnTheLoginPage();
        GivenTheUserClicksTheLink("Forgot Password?");
        // Optionally assert the forgot password form is shown
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var emailInput = wait.Until(d => d.FindElement(By.CssSelector("input[type='email'], input[name='email']")));
        Assert.IsTrue(emailInput.Displayed);
    }

    [When(@"the user enters ""(.*)"" and submits the form at T0")]
    public void WhenTheUserEntersAndSubmitsTheFormAtT0(string email)
    {
        var now = _timeProvider?.UtcNow ?? DateTime.UtcNow;
        _scenarioContext["T0"] = now;
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        var emailInput = wait.Until(d => d.FindElement(By.CssSelector("input[type='email'], input[name='email']")));
        emailInput.Clear();
        emailInput.SendKeys(email);

        // Submit button
        var submit = Driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit']")).FirstOrDefault();
        Assert.IsNotNull(submit, "Submit button not found on forgot password form.");
        submit.Click();
        _scenarioContext["ForgotPasswordSubmittedEmail"] = email;
        _scenarioContext["ForgotPasswordSubmittedAt"] = now;
    }

    [Then(@"the UI displays ""(.*)""")]
    public void ThenTheUIDisplays(string expectedMessage)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        // Look for common success message containers
        bool found = false;
        try
        {
            var el = wait.Until(d =>
            {
                var nodes = d.FindElements(By.XPath($"//*[contains(text(), \"{expectedMessage}\")]"));
                return nodes.FirstOrDefault(n => n.Displayed);
            });
            found = el != null;
        }
        catch { found = false; }
        Assert.IsTrue(found, $"Expected UI to display message '{expectedMessage}'.");
    }

    [Then(@"the UI success message is visible")]
    public void ThenTheUiSuccessMessageIsVisible()
    {
        // We consider the last shown message container present and visible
        var message = Driver.FindElements(By.CssSelector(".alert, .toast, [role='status'], .success")).FirstOrDefault();
        Assert.IsTrue(message != null && message.Displayed, "Success message not visible.");
    }

    [Then(@"the UI success message auto-hides after 3 seconds")]
    public void ThenTheUiSuccessMessageAutoHidesAfter3Seconds()
    {
        // Record presence at t, then ensure it's not visible after ~3s (+ small buffer)
        var message = Driver.FindElements(By.CssSelector(".alert, .toast, [role='status'], .success")).FirstOrDefault();
        Assert.IsNotNull(message, "Expected success message present initially.");
        Thread.Sleep(TimeSpan.FromSeconds(3.5)); // Buffer for animations
        var stillVisible = message.Displayed;
        Assert.IsFalse(stillVisible, "Success message did not auto-hide after 3 seconds.");
    }

    #endregion

    #region Email / Token extraction steps

    [Then(@"an outbound password reset email is sent to ""(.*)"" within 5 seconds of T0 \(95th-percentile\)")]
    public void ThenAnOutboundPasswordResetEmailIsSentWithin5SecondsOfT0(string email)
    {
        var t0 = _scenarioContext.ContainsKey("T0") ? (DateTime)_scenarioContext["T0"] : (_scenarioContext.ContainsKey("ForgotPasswordSubmittedAt") ? (DateTime)_scenarioContext["ForgotPasswordSubmittedAt"] : DateTime.UtcNow);
        // Query SMTP logs for the message sent within 5 seconds
        var msgs = _smtpClient?.QueryMessagesToRecipient(email, t0, t0.AddSeconds(5));
        Assert.IsTrue(msgs != null && msgs.Any(), $"No password reset email found to {email} within 5 seconds of T0.");
        // Store the most recent for downstream scenarios
        _scenarioContext["LastPasswordResetEmail"] = msgs.OrderByDescending(m => m.ReceivedAt).First();
    }

    [Given(@"a password reset email has been sent to ""(.*)""")]
    public void GivenAPasswordResetEmailHasBeenSentTo(string email)
    {
        // For robustness: if we already have one in context use it, otherwise trigger forgot flow programmatically or via UI.
        if (!_scenarioContext.ContainsKey("LastPasswordResetEmail"))
        {
            // Trigger via API or UI
            _apiClient?.TriggerPasswordReset(email);
            // Poll SMTP for email
            var msg = _smtpClient?.WaitForMessage(email, TimeSpan.FromSeconds(10));
            Assert.IsNotNull(msg, $"No password reset email observed for {email}.");
            _scenarioContext["LastPasswordResetEmail"] = msg;
        }
    }

    [When(@"the test framework extracts the reset URL from the email body")]
    public void WhenTheTestFrameworkExtractsTheResetUrlFromTheEmailBody()
    {
        var msg = _scenarioContext["LastPasswordResetEmail"] as SmtpMessage;
        Assert.IsNotNull(msg, "No last password reset email in context.");

        // Common approach: find first URL containing /reset-password or token param
        var body = msg.Body ?? string.Empty;
        var urlPattern = new Regex(@"https?://[^\s""']+/reset-password\?[^ \r\n""']*", RegexOptions.IgnoreCase);
        var m = urlPattern.Match(body);
        Assert.IsTrue(m.Success, "Reset URL not found in email body.");
        var url = m.Value;
        _scenarioContext["ResetUrl"] = url;

        // Extract token param if present
        var tokenMatch = Regex.Match(url, @"[?&]token=([^&]+)");
        if (tokenMatch.Success)
        {
            _scenarioContext["ExtractedToken"] = tokenMatch.Groups[1].Value;
        }
    }

    [Then(@"the token parameter value is exactly 32 characters long")]
    public void ThenTheTokenParameterValueIsExactly32CharactersLong()
    {
        Assert.IsTrue(_scenarioContext.ContainsKey("ExtractedToken"), "No token extracted from email.");
        var token = (string)_scenarioContext["ExtractedToken"];
        Assert.AreEqual(32, token.Length, $"Expected token length 32, actual {token.Length}");
    }

    [Then(@"the token contains only characters in the set A–Z and 0–9")]
    public void ThenTheTokenContainsOnlyCharactersInTheSetAZAnd09()
    {
        var token = (string)_scenarioContext["ExtractedToken"];
        var match = Regex.IsMatch(token, @"^[A-Z0-9]{32}$");
        Assert.IsTrue(match, $"Token '{token}' does not match required character set A-Z and 0-9.");
    }

    [Then(@"the server has a record of that token associated with the user")]
    public void ThenTheServerHasARecordOfThatTokenAssociatedWithTheUser()
    {
        var token = (string)_scenarioContext["ExtractedToken"];
        var email = (string)_scenarioContext["TestUserEmail"];
        var record = _apiClient?.GetPasswordResetRecord(token);
        Assert.IsNotNull(record, "Server has no record for token.");
        Assert.AreEqual(email, record.AssociatedEmail, "Token not associated with expected user.");
        _scenarioContext["ServerTokenRecord"] = record;
    }

    [Then(@"the server-side token record includes an expiry timestamp exactly 15 minutes after the token generation time")]
    public void ThenTheServer_SideTokenRecordIncludesAnExpiryTimestampExactly15MinutesAfterTheTokenGenerationTime()
    {
        var rec = _scenarioContext["ServerTokenRecord"] as PasswordResetRecord;
        Assert.IsNotNull(rec, "Server token record not available.");
        var created = rec.CreatedUtc;
        var expiry = rec.ExpiresUtc;
        var expected = created.AddMinutes(15);
        // Allow tiny delta for rounding
        Assert.IsTrue(Math.Abs((expiry - expected).TotalSeconds) < 2, $"Expiry timestamp {expiry:o} not exactly 15 minutes after creation {created:o}.");
    }

    #endregion

    #region Reset page interactions

    [Given(@"a valid token exists for ""(.*)"" and is unexpired")]
    public void GivenAValidTokenExistsForAndIsUnexpired(string email)
    {
        // Generate or fetch a valid token via API
        var token = _apiClient?.CreatePasswordResetToken(email);
        Assert.IsNotNull(token, "Failed to create a valid token via API.");
        var record = _apiClient.GetPasswordResetRecord(token);
        Assert.IsTrue(record != null && record.ExpiresUtc > (_time_provider?.UtcNow ?? DateTime.UtcNow), "Generated token is not unexpired.");
        _scenario_context["ValidToken"] = token;
        _scenarioContext["TestUserEmail"] = email;
        _scenarioContext["ServerTokenRecord"] = record;
    }

    [When(@"the user navigates to ""/reset-password\?token=\{token\}""")]
    public void WhenTheUserNavigatesToReset_PasswordTokenToken()
    {
        // This step expects token in context under "ValidToken" or use ExtractedToken
        var token = _scenarioContext.ContainsKey("ValidToken") ? (string)_scenarioContext["ValidToken"] : _scenarioContext.ContainsKey("ExtractedToken") ? (string)_scenarioContext["ExtractedToken"] : null;
        Assert.IsNotNull(token, "No token available to navigate with.");
        var url = _apiClient?.GetAppUrl($"/reset-password?token={token}") ?? $"/reset-password?token={token}";
        Driver.Navigate().GoToUrl(url);
        _scenarioContext["CurrentTokenUsedInBrowser"] = token;
    }

    [Then(@"the reset password form is displayed")]
    public void ThenTheResetPasswordFormIsDisplayed()
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var form = wait.Until(d => d.FindElement(By.CssSelector("form#reset-password, form[name='reset-password'], form[data-test='reset-password']")));
        Assert.IsTrue(form.Displayed, "Reset password form not displayed.");
    }

    [Then(@"a hidden form field contains the same token value")]
    public void ThenAHiddenFormFieldContainsTheSameTokenValue()
    {
        var token = _scenarioContext["CurrentTokenUsedInBrowser"] as string ?? _scenarioContext["ValidToken"] as string;
        Assert.IsNotNull(token, "Token not found in scenario context.");
        var hidden = Driver.FindElements(By.CssSelector("input[type='hidden'][name='token'], input[name='token']")).FirstOrDefault();
        Assert.IsNotNull(hidden, "Hidden token field not found on form.");
        var val = hidden.GetAttribute("value");
        Assert.AreEqual(token, val, "Hidden token value does not match token used in URL.");
    }

    [When(@"the user submits a new password that meets policy ""(.*)"" \(>=8 chars, 1 uppercase, 1 number, 1 special\)")]
    public void WhenTheUserSubmitsANewPasswordThatMeetsPolicy(string newPassword)
    {
        SubmitNewPasswordOnForm(newPassword);
    }

    private void SubmitNewPasswordOnForm(string newPassword)
    {
        var passwordInput = Driver.FindElements(By.CssSelector("input[type='password'][name='password'], input[name='newPassword']")).FirstOrDefault();
        var confirmInput = Driver.FindElements(By.CssSelector("input[type='password'][name='confirmPassword'], input[name='confirm']")).FirstOrDefault();
        Assert.IsNotNull(passwordInput, "Password input not found on reset form.");
        passwordInput.Clear();
        passwordInput.SendKeys(newPassword);
        if (confirmInput != null)
        {
            confirmInput.Clear();
            confirmInput.SendKeys(newPassword);
        }
        var submitBtn = Driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit']")).FirstOrDefault();
        Assert.IsNotNull(submitBtn, "Submit button not found on reset form.");
        submit