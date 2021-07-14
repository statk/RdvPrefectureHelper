using System;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace rdvprefecture
{
    class Program
    {
        // URL's and element Id's might be different according to your Prefecture web site
        private const string PrefectureSiteUri = "https://www.hauts-de-seine.gouv.fr/booking/create/14086/0";
        private const string ConditionElementId = "condition";
        private const string FirstPageNextButtonElementId = "nextButton";
        private const string WindowRatioButtonId = "radio";
        private const string WindowNextButtonId = "Bbutton";

        static void Main(string[] args)
        {
            ChromeDriver driver;
            bool shouldContinue;
            var itteration = 0;
            var random = new Random();

            do
            {
                driver = new ChromeDriver();
                Console.WriteLine($"{++itteration}:{DateTime.Now}");
                try
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        GoToRdvPage(driver, i);
                         Thread.Sleep(random.Next(100, 1000));
                    }

                    var source = driver.PageSource;

                    // Retrying in case when all timeslots are blocked
                    shouldContinue = driver.PageSource.Contains("Il n'existe plus de plage horaire libre pour votre demande de rendez-vous. Veuillez recommencer ultérieurement.")
                                    || ShouldRetry(driver);
                }
                catch (Exception ex)
                {
                    // Log exception
                    using (StreamWriter outputFile = new StreamWriter($"exception{DateTime.Now.Ticks}.txt"))
                        outputFile.WriteLine(ex.Message);

                    shouldContinue = true;
                }
                if (shouldContinue)
                {
                    driver?.Quit();
                    driver = null;
                    GC.Collect();
                    Thread.Sleep(120000);
                }
            } while (shouldContinue);

            var shot = driver.GetScreenshot();
            shot.SaveAsFile($"plage-at-{DateTime.Now.Ticks}.png");

            using (StreamWriter outputFile = new StreamWriter($"source{DateTime.Now.Ticks}.txt"))
                outputFile.WriteLine(driver.PageSource);

            const string accountSid = "{Twilio account sid}";
            const string authToken = "{Twilio authentication token}";

            TwilioClient.Init(accountSid, authToken);

            MessageResource.Create(
                body: "Rdv found!",
                from: new Twilio.Types.PhoneNumber("{Twilio phone number}"),
                to: new Twilio.Types.PhoneNumber("{Phone number of the person who will receive sms")
            );

            Console.Read();
        }

        private static void GoToRdvPage(ChromeDriver driver, int page, int retryCount = 0)
        {
            // Number of retries in case of error before pause between tries.
            if (retryCount == 3)
                return;

            // Pause before new retry
            if (retryCount != 0)
                Thread.Sleep(20000);

            // Random pause after each retry
            var random = new Random();

            try
            {
                // State of RDV process pages
                switch (page)
                {
                    case 1:
                        FirstPage(driver);
                        break;
                    case 2:
                        SecondPage(driver);
                        break;
                    default: return;
                };

                // Check if the page body contains error messages
                if (ShouldRetry(driver) && page != 2)
                {
                    driver.Navigate().Refresh();
                    retryCount ++;
                    GoToRdvPage(driver, page, retryCount);
                }
            }
            catch(Exception ex)
            {
                // Logging exceptions 
                Console.WriteLine($"{DateTime.Now}, page {page}, exception: {ex}");

                // Retrying if needed
                if (ShouldRetry(driver) && page != 2)
                {
                    driver.Navigate().Refresh();
                    retryCount ++;
                    GoToRdvPage(driver, page, retryCount);
                    return;
                }
                else
                    throw;
            }

            // Delay between the retries 
            Thread.Sleep(random.Next(100, 1000));
        }

        private static bool ShouldRetry(ChromeDriver driver)
        {
            // Check if the page contains error messages in the bodt
            return driver.PageSource.Contains("500 Internal Server Error")
                                ||driver.PageSource.Contains("502 Bad Gateway")
                                || driver.PageSource.Contains("503 Service Unavailable")
                                || driver.PageSource.Contains("400 Bad Request")
                                || driver.PageSource.Contains("401 Unauthorized")
                                || driver.PageSource.Contains("403 Forbidden")
                                || driver.PageSource.Contains("404 Not Found");
        }

        
        private static void SecondPage(ChromeDriver driver)
        {
            // Navigating insade the second page
            // Takng the first window
            var radioButton = driver.FindElementByClassName(WindowRatioButtonId);
            radioButton.Click();

            // Going to the next page
            var submitButton = driver.FindElementByClassName(WindowNextButtonId);
            submitButton.Click();
        }

        //
        private static void FirstPage(ChromeDriver driver)
        {
            // Open the browser and navigation to the site
            var js = (IJavaScriptExecutor)driver;
            driver.Url = PrefectureSiteUri;
            const string script = "arguments[0].scrollIntoView(true);";

            // Agreeing with conditions
            var checkBox = driver.FindElementById(ConditionElementId);
            js.ExecuteScript(script, checkBox);

            // Small delay detween the actions 
            var random = new Random();
            Thread.Sleep(random.Next(100, 1000));
 
            checkBox.Click();

            // Small delay detween the actions 
            Thread.Sleep(random.Next(100, 1000));

            // Navigating to the next page 
            var button = driver.FindElementByName(FirstPageNextButtonElementId);
            button.Click();
        }
    }
}
