using TechTalk.SpecFlow;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

[Binding]
public class SearchSteps
{
    private IWebDriver driver;

    [Given(@"I navigate to the homepage")]
    public void GivenINavigateToTheHomepage()
    {
        driver = new ChromeDriver();
        driver.Navigate().GoToUrl("https://example.com");
    }

    [When(@"I enter search term ""([^"]*)""")]
    public void WhenIEnterSearchTerm(string searchTerm)
    {
        driver.FindElement(By.Id("search-box")).SendKeys(searchTerm);
    }
}
