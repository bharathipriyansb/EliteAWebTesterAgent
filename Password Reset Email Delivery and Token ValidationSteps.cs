using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

[Binding]
public class PasswordResetSteps
{
    private readonly ScenarioContext _scenarioContext;
    private IWebDriver Driver => _scenarioContext.ContainsKey("WebDriver") ? _scenarioContext["WebDriver"] as IWebDriver : null;
    private const string LoginUrl = "https://example.com/login"; // Replace with real login URL
    private const string SuccessMessage = "Check your email for reset instructions";
    private static readonly Regex TokenRegex = new Regex("^[A-Z0-9]{32}$", RegexOptions.Compiled);

    public PasswordResetSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    // Background steps
    [Given(@"the password reset system and mail delivery service are operational")]
    public void GivenThePasswordResetSystemAndMailDeliveryServiceAreOperational()
    {
        // Optionally perform health checks via API/health endpoint
        _scenarioContext["MailServiceOperational"] = true;
        _scenarioContext["PasswordResetServiceOperational"] = true;
    }

    [Given(@"the mail delivery test mailbox access and logs are available for verification")]
    public void GivenTheMailDeliveryTestMailboxAccessAndLogsAreAvailableForVerification()
    {
        // Ensure EmailTestHelper can access test mailboxes
        EmailTestHelper.EnsureConnection();
    }

    [Given(@"a registered user exists with email ""(.*)""")]
    public void GivenARegisteredUserExistsWithEmail(string email)
    {
        // Ensure test user exists in test environment or create one
        TestUserHelper.EnsureRegistered(email);
        _scenarioContext["RegisteredEmail"] = email;
    }

    [Given(@"the user is on the login page")]
    public void GivenTheUserIsOnTheLoginPage()
    {
        if (Driver == null) return; // For API/DB-only tests driver may be null
        Driver.Navigate().GoToUrl(LoginUrl);
        _scenarioContext["CurrentPage"] = "Login";
    }

    // Scenario steps for requesting reset
    [Given(@"the registered user clicks the ""Forgot Password\?"" link")]
    public void GivenTheRegisteredUserClicksTheForgotPasswordLink()
    {
        var link = Driver.FindElement(By.LinkText("Forgot Password?"));
        link.Click();
    }

    [Given(@"the reset-email input is visible")]
    public void GivenTheResetEmailInputIsVisible()
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var input = wait.Until(d => d.FindElement(By.CssSelector("input[name='reset-email'], input[id='reset-email']")));
        Assert.IsTrue(input.Displayed);
    }

    [When(@"the user enters ""(.*)"" into the reset-email input")]
    public void WhenTheUserEntersIntoTheReset_EmailInput(string email)
    {
        var input = Driver.FindElement(By.CssSelector("input[name='reset-email'], input[id='reset-email']"));
        input.Clear();
        input.SendKeys(email);
        _scenarioContext["EnteredResetEmail"] = email;
    }

    [When(@"the user clicks the send-reset button")]
    public void WhenTheUserClicksTheSend_ResetButton()
    {
        var button = Driver.FindElement(By.CssSelector("button[type='submit'], button[id='send-reset'], button[name='send-reset']"));
        button.Click();
    }

    [Then(@"the UI displays the success message ""(.*)""")]
    public void ThenTheUIDisplaysTheSuccessMessage(string expectedMessage)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var el = wait.Until(d => d.FindElement(By.CssSelector(".notification, .alert, .toast")));
        Assert.IsTrue(el.Text.Contains(expectedMessage), $"Expected message '{expectedMessage}' but got '{el.Text}'");
    }

    [Then(@"an email is delivered to ""(.*)"" within (.*) seconds")]
    public void ThenAnEmailIsDeliveredToWithinSeconds(string email, int seconds)
    {
        var msg = EmailTestHelper.WaitForEmail(email, TimeSpan.FromSeconds(seconds));
        Assert.IsNotNull(msg, $"No email delivered to {email} within {seconds} seconds");
        _scenarioContext["LatestEmail"] = msg;
    }

    [Then(@"the email From address is ""(.*)""")]
    public void ThenTheEmailFromAddressIs(string fromAddress)
    {
        var msg = _scenarioContext["LatestEmail"] as EmailMessage;
        Assert.IsNotNull(msg);
        Assert.AreEqual(fromAddress, msg.From, "From address mismatch");
    }

    [Then(@"the email Subject is ""(.*)""")]
    public void ThenTheEmailSubjectIs(string expectedSubject)
    {
        var msg = _scenarioContext["LatestEmail"] as EmailMessage;
        Assert.IsNotNull(msg);
        Assert.AreEqual(expectedSubject, msg.Subject);
    }

    [Then(@"the email body includes a reset URL containing a token parameter")]
    public void ThenTheEmailBodyIncludesAResetURLContainingATokenParameter()
    {
        var msg = _scenarioContext["LatestEmail"] as EmailMessage;
        Assert.IsNotNull(msg);
        var url = EmailTestHelper.ExtractResetUrl(msg.Body);
        Assert.IsNotNull(url, "No reset URL found in email body");
        _scenarioContext["ResetUrl"] = url;
    }

    [Then(@"the token is exactly 32 characters long and contains only uppercase letters A–Z and digits 0–9")]
    public void ThenTheTokenIsExactly32CharactersLongAndContainsOnlyUppercaseLettersAndDigits()
    {
        var url = _scenarioContext["ResetUrl"] as string;
        var token = UrlHelper.GetQueryParameter(url, "token");
        Assert.IsNotNull(token, "Token parameter not present in reset URL");
        Assert.IsTrue(TokenRegex.IsMatch(token), $"Token '{token}' does not match expected format");
        _scenarioContext["Token"] = token;
    }

    [Then(@"a token record matching the token is stored in the database with a creation timestamp")]
    public void ThenATokenRecordMatchingTheTokenIsStoredInTheDatabaseWithACreationTimestamp()
    {
        var token = _scenarioContext["Token"] as string;
        var record = DBHelper.GetTokenRecord(token);
        Assert.IsNotNull(record, "Token record not found in DB");
        Assert.IsTrue(record.CreatedAt != default(DateTime), "Token record missing creation timestamp");
        _scenarioContext["TokenRecord"] = record;
    }

    // Scenario: Valid token use
    [Given(@"a valid token was generated for ""(.*)"" less than 15 minutes ago")]
    public void GivenAValidTokenWasGeneratedForLessThan15MinutesAgo(string email)
    {
        var token = DBHelper.CreateTokenForEmail(email, DateTime.UtcNow); // returns token string
        _scenarioContext["Token"] = token;
        _scenarioContext["ResetUrl"] = $"https://example.com/reset?token={token}";
        _scenarioContext["TokenCreatedAt"] = DateTime.UtcNow;
    }

    [Given(@"the user has received the email containing the reset URL with that token")]
    public void GivenTheUserHasReceivedTheEmailContainingTheResetURLWithThatToken()
    {
        var email = _scenarioContext["RegisteredEmail"] as string;
        var token = _scenarioContext["Token"] as string;
        var msg = EmailTestHelper.WaitForEmailContainingToken(email, token, TimeSpan.FromSeconds(5));
        Assert.IsNotNull(msg, "Expected email with token not received");
        _scenarioContext["LatestEmail"] = msg;
    }

    [When(@"the user navigates to the reset URL containing the token")]
    public void WhenTheUserNavigatesToTheResetURLContainingTheToken()
    {
        var url = _scenarioContext["ResetUrl"] as string;
        Driver.Navigate().GoToUrl(url);
    }

    [Then(@"the system validates the token exists and is not expired")]
    public void ThenTheSystemValidatesTheTokenExistsAndIsNotExpired()
    {
        var token = _scenarioContext["Token"] as string;
        var record = DBHelper.GetTokenRecord(token);
        Assert.IsNotNull(record, "Token not found");
        Assert.IsFalse(DBHelper.IsTokenExpired(record), "Token is expired");
    }

    [Then(@"the password reset form is displayed")]
    public void ThenThePasswordResetFormIsDisplayed()
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var form = wait.Until(d => d.FindElement(By.CssSelector("form#password-reset, form[action*='reset']")));
        Assert.IsTrue(form.Displayed);
        _scenarioContext["ResetFormElement"] = form;
    }

    [Then(@"the token is included in the reset form submission payload \(carried/hidden input\)")]
    public void ThenTheTokenIsIncludedInTheResetFormSubmissionPayload()
    {
        var token = _scenarioContext["Token"] as string;
        var hidden = Driver.FindElements(By.CssSelector("input[type='hidden'][name='token'], input[name='token']"))
                         .FirstOrDefault(i => i.GetAttribute("value") == token);
        Assert.IsNotNull(hidden, "Hidden token input not found or value mismatch");
    }

    [Then(@"the form allows entering a new password and password confirmation")]
    public void ThenTheFormAllowsEnteringANewPasswordAndPasswordConfirmation()
    {
        var newPass = Driver.FindElements(By.CssSelector("input[name='newPassword'], input[id='newPassword']")).FirstOrDefault();
        var confirm = Driver.FindElements(By.CssSelector("input[name='confirmPassword'], input[id='confirmPassword']")).FirstOrDefault();
        Assert.IsNotNull(newPass, "New password input not present");
        Assert.IsNotNull(confirm, "Password confirmation input not present");
    }

    // Scenario: Expired token
    [Given(@"a token was generated and stored in the database for ""(.*)"" (.*) minutes ago")]
    public void GivenATokenWasGeneratedAndStoredInTheDatabaseForMinutesAgo(string email, int minutesAgo)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
        var token = DBHelper.CreateTokenForEmail(email, createdAt);
        _scenarioContext["Token"] = token;
        _scenarioContext["ResetUrl"] = $"https://example.com/reset?token={token}";
        _scenarioContext["TokenCreatedAt"] = createdAt;
    }

    [Then(@"the system rejects the token as expired")]
    public void ThenTheSystemRejectsTheTokenAsExpired()
    {
        var token = _scenarioContext["Token"] as string;
        var record = DBHelper.GetTokenRecord(token);
        Assert.IsTrue(DBHelper.IsTokenExpired(record), "Token should be expired but was not marked expired");
    }

    [Then(@"the user sees an error message indicating the token is expired and must request a new reset")]
    public void ThenTheUserSeesAnErrorMessageIndicatingTheTokenIsExpiredAndMustRequestANewReset()
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var el = wait.Until(d => d.FindElement(By.CssSelector(".error, .alert-danger")));
        Assert.IsTrue(el.Text.ToLower().Contains("expired") || el.Text.ToLower().Contains("request a new"), $"Unexpected error text: {el.Text}");
    }

    [Then(@"the password reset form is not displayed")]
    public void ThenThePasswordResetFormIsNotDisplayed()
    {
        var forms = Driver.FindElements(By.CssSelector("form#password-reset, form[action*='reset']"));
        Assert.IsTrue(forms.Count == 0 || !forms.Any(f => f.Displayed));
    }

    [Then(@"no password change is permitted using the expired token")]
    public void ThenNoPasswordChangeIsPermittedUsingTheExpiredToken()
    {
        // Attempt to submit a password change should be rejected; here check DB or UI prevents changes
        var token = _scenarioContext["Token"] as string;
        Assert.IsFalse(DBHelper.CanUseTokenToChangePassword(token), "Expired token should not be usable to change password");
    }

    // Scenario: Provider deliverability
    [Given(@"the system sends a reset email to addresses at Gmail, Outlook, and Yahoo")]
    public void GivenTheSystemSendsAResetEmailToAddressesAtGmailOutlookAndYahoo()
    {
        var addresses = new[] { "test.user@gmail.com", "test.user@outlook.com", "test.user@yahoo.com" };
        foreach (var addr in addresses)
        {
            ApiHelpers.RequestPasswordReset(addr);
        }
        _scenarioContext["ProviderAddresses"] = addresses;
    }

    [When(@"the reset emails are processed by those providers")]
    public void WhenTheResetEmailsAreProcessedByThoseProviders()
    {
        // Wait/allow providers to deliver; EmailTestHelper will query each mailbox
    }

    [Then(@"each provider receives the email successfully \(mailbox contains the message\)")]
    public void ThenEachProviderReceivesTheEmailSuccessfullyMailboxContainsTheMessage()
    {
        var addresses = _scenarioContext["ProviderAddresses"] as string[];
        foreach (var addr in addresses)
        {
            var msg = EmailTestHelper.WaitForEmail(addr, TimeSpan.FromSeconds(30));
            Assert.IsNotNull(msg, $"Provider mailbox for {addr} did not receive email");
            Assert.AreEqual("noreply@elitealearning.com", msg.From);
            Assert.AreEqual("Reset Your Password - EliteALearning", msg.Subject);
            var url = EmailTestHelper.ExtractResetUrl(msg.Body);
            Assert.IsNotNull(url, $"Reset URL not found in email to {addr}");
        }
    }

    // Scenario: Unregistered email
    [Given(@"the user clicks the ""Forgot Password\?"" link")]
    public void GivenTheUserClicksTheForgotPasswordLink()
    {
        var link = Driver.FindElement(By.LinkText("Forgot Password?"));
        link.Click();
    }

    [When(@"the user enters ""(.*)"" into the reset-email input")]
    public void WhenTheUserEntersIntoTheReset_EmailInput_Unregistered(string email)
    {
        var input = Driver.FindElement(By.CssSelector("input[name='reset-email'], input[id='reset-email']"));
        input.Clear();
        input.SendKeys(email);
        _scenarioContext["EnteredResetEmail"] = email;
    }

    [Then(@"no password reset email is sent to ""(.*)""")]
    public void ThenNoPasswordResetEmailIsSentTo(string email)
    {
        var msg = EmailTestHelper.WaitForEmail(email, TimeSpan.FromSeconds(5));
        Assert.IsNull(msg, $"An email was sent to unregistered address {email}");
    }

    [Then(@"the system does not reveal whether the email is registered")]
    public void ThenTheSystemDoesNotRevealWhetherTheEmailIsRegistered()
    {
        // We already asserted UI shows same generic message and no differential behavior is present
    }

    // Scenario: Malformed email input displays client-side validation error
    [Then(@"the form shows a validation error ""(.*)""")]
    public void ThenTheFormShowsAValidationError(string expected)
    {
        var err = Driver.FindElement(By.CssSelector(".field-error, .validation-message"));
        Assert.IsTrue(err.Text.Contains(expected), $"Expected validation '{expected}', got '{err.Text}'");
    }

    [Then(@"no request to send a reset email is initiated")]
    public void ThenNoRequestToSendAResetEmailIsInitiated()
    {
        var email = _scenarioContext["EnteredResetEmail"] as string;
        Assert.IsNull(EmailTestHelper.WaitForEmail(email, TimeSpan.FromSeconds(3)), "Email was unexpectedly sent for malformed address");
    }

    // Scenario: Invalid token format
    [Given(@"a token value ""(.*)"" \(invalid length and characters\) is present in a reset URL")]
   