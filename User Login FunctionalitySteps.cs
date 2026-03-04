using TechTalk.SpecFlow;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[Binding]
public class LoginSteps
{
    private IWebDriver driver;

    [Given(@"I navigate to the login page")]
    public void GivenINavigateToTheLoginPage()
    {
        driver = new ChromeDriver();
        driver.Navigate().GoToUrl("https://example.com/login");
    }

    [When(@"I enter username ""([^"]*)""")]
    public void WhenIEnterUsername(string username)
    {
        driver.FindElement(By.Id("username")).SendKeys(username);
    }
