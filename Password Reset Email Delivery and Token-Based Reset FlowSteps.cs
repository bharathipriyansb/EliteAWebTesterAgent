using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Safari;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

namespace PasswordResetTests.StepDefinitions
{
    [Binding]
    public class PasswordResetSteps
    {
        private readonly ScenarioContext _scenarioContext;
        private IWebDriver _driver;
        private HttpClient _httpClient;
        private HttpResponseMessage _apiResponse;
        private string _baseUrl = "https://elitealearning.com";
        private string _apiBaseUrl = "https://api.elitealearning.com";
        private Stopwatch _stopwatch;
        private MockEmailService _mockEmailService;
        private MockTimeService _mockTimeService;
        private MockDatabaseService _mockDatabaseService;
        private MockAuditService _mockAuditService;

        public PasswordResetSteps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _mockEmailService = new MockEmailService();
            _mockTimeService = new MockTimeService();
            _mockDatabaseService = new MockDatabaseService();
            _mockAuditService = new MockAuditService();
        }

        #region Background Steps

        [Given(@"the password reset service, API, and email delivery service are operational")]
        public void GivenThePasswordResetServiceAPIAndEmailDeliveryServiceAreOperational()
        {
            // Health check endpoints
            var healthCheckResponse = _httpClient.GetAsync($"{_apiBaseUrl}/health").Result;
            Assert.AreEqual(HttpStatusCode.OK, healthCheckResponse.StatusCode, "Services are not operational");
            
            // Initialize mock services
            _mockEmailService.Initialize();
            _mockDatabaseService.Initialize();
            _scenarioContext["EmailService"] = _mockEmailService;
            _scenarioContext["DatabaseService"] = _mockDatabaseService;
        }

        [Given(@"HTTPS is enforced for all endpoints")]
        public void GivenHTTPSIsEnforcedForAllEndpoints()
        {
            // Verify HTTPS enforcement by attempting HTTP request
            var httpUrl = _baseUrl.Replace("https://", "http://");
            var httpResponse = _httpClient.GetAsync(httpUrl).Result;
            
            // Should redirect to HTTPS (301 or 302) or refuse connection
            Assert.IsTrue(
                httpResponse.StatusCode == HttpStatusCode.MovedPermanently ||
                httpResponse.StatusCode == HttpStatusCode.Redirect ||
                httpResponse.RequestMessage.RequestUri.Scheme == "https",
                "HTTPS is not properly enforced"
            );
        }

        [Given(@"the test environment clock can be controlled \(mockable\) for token expiry tests")]
        public void GivenTheTestEnvironmentClockCanBeControlledForTokenExpiryTests()
        {
            _mockTimeService.Initialize();
            _scenarioContext["TimeService"] = _mockTimeService;
        }

        [Given(@"there exists a registered user \"(.*)\" with a known account")]
        public void GivenThereExistsARegisteredUserWithAKnownAccount(string email)
        {
            _mockDatabaseService.AddRegisteredUser(email);
            _scenarioContext["RegisteredUser"] = email;
        }

        #endregion

        #region Scenario 1: Registered user requests password reset and receives reset email within SLA

        [Given(@"the user is on the \"(.*)\" page")]
        public void GivenTheUserIsOnThePage(string pageName)
        {
            _driver = new ChromeDriver();
            _driver.Manage().Window.Maximize();
            _driver.Navigate().GoToUrl($"{_baseUrl}/forgot-password");
            _scenarioContext["Driver"] = _driver;
            
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.CssSelector("input[name='email']")).Displayed);
        }

        [Given(@"the user is a registered account holder with email \"(.*)\"")]
        public void GivenTheUserIsARegisteredAccountHolderWithEmail(string email)
        {
            Assert.IsTrue(_mockDatabaseService.IsUserRegistered(email), $"User {email} is not registered");
            _scenarioContext["UserEmail"] = email;
        }

        [When(@"the user submits \"(.*)\" to POST /api/forgot-password from IP \"(.*)\"")]
        public void WhenTheUserSubmitsToPostApiForgotPasswordFromIP(string email, string ipAddress)
        {
            _stopwatch = Stopwatch.StartNew();
            
            var payload = new
            {
                email = email
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );
            
            _httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", ipAddress);
            _apiResponse = _httpClient.PostAsync($"{_apiBaseUrl}/api/forgot-password", content).Result;
            
            _scenarioContext["ApiResponse"] = _apiResponse;
            _scenarioContext["RequestIP"] = ipAddress;
            _scenarioContext["RequestEmail"] = email;
        }

        [Then(@"the UI displays \"(.*)\"")]
        public void ThenTheUIDisplays(string expectedMessage)
        {
            if (_driver != null)
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                var messageElement = wait.Until(d => d.FindElement(By.CssSelector(".message, .alert, [data-testid='message']")));
                Assert.IsTrue(messageElement.Text.Contains(expectedMessage), 
                    $"Expected message '{expectedMessage}' not found. Actual: '{messageElement.Text}'");
            }
        }

        [Then(@"the system sends an email to \"(.*)\" from \"(.*)\"")]
        public void ThenTheSystemSendsAnEmailToFrom(string toEmail, string fromEmail)
        {
            Thread.Sleep(500); // Allow async email processing
            var sentEmail = _mockEmailService.GetLastEmailSentTo(toEmail);
            
            Assert.IsNotNull(sentEmail, $"No email was sent to {toEmail}");
            Assert.AreEqual(fromEmail, sentEmail.From, $"Email from address mismatch");
            Assert.AreEqual(toEmail, sentEmail.To, $"Email to address mismatch");
            
            _scenarioContext["SentEmail"] = sentEmail;
        }

        [Then(@"the email subject is \"(.*)\"")]
        public void ThenTheEmailSubjectIs(string expectedSubject)
        {
            var sentEmail = _scenarioContext["SentEmail"] as MockEmail;
            Assert.IsNotNull(sentEmail);
            Assert.AreEqual(expectedSubject, sentEmail.Subject, "Email subject mismatch");
        }

        [Then(@"the email is delivered within (.*) seconds")]
        public void ThenTheEmailIsDeliveredWithinSeconds(int maxSeconds)
        {
            _stopwatch.Stop();
            var deliveryTime = _stopwatch.Elapsed.TotalSeconds;
            Assert.LessOrEqual(deliveryTime, maxSeconds, 
                $"Email delivery SLA breached. Expected: <={maxSeconds}s, Actual: {deliveryTime}s");
            
            _scenarioContext["EmailDeliveryTime"] = deliveryTime;
        }

        [Then(@"the email contains a reset URL with a query parameter token that is exactly (.*) characters long and contains only uppercase A–Z and digits 0–9")]
        public void ThenTheEmailContainsAResetURLWithValidToken(int tokenLength)
        {
            var sentEmail = _scenarioContext["SentEmail"] as MockEmail;
            Assert.IsNotNull(sentEmail);
            
            // Extract token from email body
            var tokenRegex = new Regex(@"token=([A-Z0-9]{32})");
            var match = tokenRegex.Match(sentEmail.Body);
            
            Assert.IsTrue(match.Success, "Reset token not found in email");
            
            var token = match.Groups[1].Value;
            Assert.AreEqual(tokenLength, token.Length, $"Token length mismatch. Expected: {tokenLength}, Actual: {token.Length}");
            Assert.IsTrue(Regex.IsMatch(token, @"^[A-Z0-9]+$"), 
                "Token contains invalid characters. Must be uppercase A-Z and digits 0-9 only");
            
            _scenarioContext["ResetToken"] = token;
        }

        [Then(@"the generated token is stored server-side with a (.*)-minute expiry associated to the correct user")]
        public void ThenTheGeneratedTokenIsStoredServerSideWithExpiryAssociatedToTheCorrectUser(int expiryMinutes)
        {
            var token = _scenarioContext["ResetToken"] as string;
            var email = _scenarioContext["RequestEmail"] as string;
            
            var storedToken = _mockDatabaseService.GetTokenByValue(token);
            Assert.IsNotNull(storedToken, "Token not found in database");
            Assert.AreEqual(email, storedToken.UserEmail, "Token not associated with correct user");
            Assert.IsTrue(storedToken.IsValid, "Token is not valid");
            
            var expectedExpiry = DateTime.UtcNow.AddMinutes(expiryMinutes);
            var timeDifference = Math.Abs((storedToken.ExpiryTime - expectedExpiry).TotalSeconds);
            Assert.LessOrEqual(timeDifference, 5, "Token expiry time is not within expected range");
        }

        #endregion

        #region Scenario 2: Unregistered email submission

        [Given(@"a non-registered email \"(.*)\"")]
        public void GivenANonRegisteredEmail(string email)
        {
            Assert.IsFalse(_mockDatabaseService.IsUserRegistered(email), $"Email {email} should not be registered");
            _scenarioContext["UnregisteredEmail"] = email;
        }

        [When(@"the user submits \"(.*)\" to POST /api/forgot-password")]
        public void WhenTheUserSubmitsToPostApiForgotPassword(string email)
        {
            var payload = new
            {
                email = email
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );
            
            _apiResponse = _httpClient.PostAsync($"{_apiBaseUrl}/api/forgot-password", content).Result;
            _scenarioContext["ApiResponse"] = _apiResponse;
        }

        [Then(@"no password reset email is sent to \"(.*)\"")]
        public void ThenNoPasswordResetEmailIsSentTo(string email)
        {
            Thread.Sleep(500);
            var sentEmail = _mockEmailService.GetLastEmailSentTo(email);
            Assert.IsNull(sentEmail, $"Email should not have been sent to {email}");
        }

        [Then(@"the system does not create a stored reset token for that address")]
        public void ThenTheSystemDoesNotCreateAStoredResetTokenForThatAddress()
        {
            var email = _scenarioContext["UnregisteredEmail"] as string;
            var tokens = _mockDatabaseService.GetTokensByEmail(email);
            Assert.IsEmpty(tokens, "No tokens should exist for unregistered email");
        }

        #endregion

        #region Scenario 3: Rate limiting

        [Given(@"the client IP \"(.*)\" has already submitted POST /api/forgot-password (.*) times in the last hour")]
        public void GivenTheClientIPHasAlreadySubmittedPostApiForgotPasswordTimesInTheLastHour(string ipAddress, int requestCount)
        {
            for (int i = 0; i < requestCount; i++)
            {
                var payload = new { email = "test@example.com" };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                
                _httpClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
                _httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", ipAddress);
                _httpClient.PostAsync($"{_apiBaseUrl}/api/forgot-password", content).Wait();
            }
            
            _scenarioContext["RateLimitIP"] = ipAddress;
        }

        [When(@"the client IP \"(.*)\" submits another POST /api/forgot-password within the same hour")]
        public void WhenTheClientIPSubmitsAnotherPostApiForgotPasswordWithinTheSameHour(string ipAddress)
        {
            var payload = new { email = "test@example.com" };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );
            
            _httpClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
            _httpClient.DefaultRequestHeaders.Add("X-Forwarded-For", ipAddress);
            _apiResponse = _httpClient.PostAsync($"{_apiBaseUrl}/api/forgot-password", content).Result;
            
            _scenarioContext["ApiResponse"] = _apiResponse;
        }

        [Then(@"the system returns HTTP (.*) Too Many Requests")]
        public void ThenTheSystemReturnsHTTPTooManyRequests(int expectedStatusCode)
        {
            var response = _scenarioContext["ApiResponse"] as HttpResponseMessage;
            Assert.AreEqual((HttpStatusCode)expectedStatusCode, response.StatusCode);
            Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        [Then(@"the response includes a human-readable message indicating rate limit exceeded and retry window")]
        public void ThenTheResponseIncludesAHumanReadableMessageIndicatingRateLimitExceededAndRetryWindow()
        {
            var response = _scenarioContext["ApiResponse"] as HttpResponseMessage;
            var responseBody = response.Content.ReadAsStringAsync().Result;
            
            Assert.IsTrue(
                responseBody.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                responseBody.Contains("too many requests", StringComparison.OrdinalIgnoreCase),
                "Response should contain rate limit message"
            );
            
            Assert.IsTrue(
                responseBody.Contains("retry", StringComparison.OrdinalIgnoreCase) ||
                responseBody.Contains("try again", StringComparison.OrdinalIgnoreCase),
                "Response should contain retry window information"
            );
        }

        [Then(@"the UI displays a clear instruction to retry later without disclosing internal rate values")]
        public void ThenTheUIDisplaysAClearInstructionToRetryLaterWithoutDisclosingInternalRateValues()
        {
            var response = _scenarioContext["ApiResponse"] as HttpResponseMessage;
            var responseBody = response.Content.ReadAsStringAsync().Result;
            
            Assert.IsFalse(responseBody.Contains("5 times") || responseBody.Contains("per hour"),
                "Response should not disclose specific rate limit values");
        }

        #endregion

        #region Scenario 4: Reset URL with valid token loads reset form

        [Given(@"a valid reset token T \(32 uppercase alphanumeric\) was generated less than (.*) minutes ago for \"(.*)\"")]
        public void GivenAValidResetTokenTWasGeneratedLessThanMinutesAgoFor(int minutesAgo, string email)
        {
            var token = GenerateToken(32);
            var expiryTime = DateTime.UtcNow.AddMinutes(15 - minutesAgo);
            
            _mockDatabaseService.StoreToken(new MockToken
            {
                TokenValue = token,
                UserEmail = email,
                ExpiryTime = expiryTime,
                IsValid = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
            });
            
            _scenarioContext["ValidToken"] = token;
        }

        [When(@"the user opens GET /reset-password\?token=T over HTTPS")]
        public void WhenTheUserOpensGETResetPasswordWithTokenOverHTTPS()
        {
            var token = _scenarioContext["ValidToken"] as string;
            _driver = new ChromeDriver();
            _driver.Manage().Window.Maximize();
            _driver.Navigate().GoToUrl($"{_baseUrl}/reset-password?token={token}");
            _scenarioContext["Driver"]