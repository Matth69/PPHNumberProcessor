using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Lottery Results Downloader");

        // Get user input for date range
        ;
        string startDate = "Enter the beginning date (yyyy-mm-dd): ";

        string endDate = "Enter the ending date (yyyy-mm-dd): ";

        // Download and parse lottery results
        await DownloadLotteryResults(startDate, endDate);

       
    }

    static async Task DownloadLotteryResults(string startDate, string endDate)
    {
        using (HttpClient httpClient = new HttpClient())
        {
            // Replace the URL with the actual URL of the lottery results website
            string url = "https://www.nationallottery.co.za/=";

            try
            {
                string htmlContent = await httpClient.GetStringAsync(url);

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Extract and process the lottery numbers
                // You will need to inspect the HTML structure of the website and adapt this code accordingly
                // In this example, I'm assuming the lottery numbers are in a table with the class "lottery-results-table"
                var table = doc.DocumentNode.SelectSingleNode("//table[@class='lottery-results-table']");

                if (table != null)
                {
                    foreach (var row in table.SelectNodes(".//tr"))
                    {
                        var columns = row.SelectNodes(".//td");
                        if (columns != null && columns.Count >= 2)
                        {
                            string date = columns[0].InnerText.Trim();
                            string numbers = columns[1].InnerText.Trim();

                            // Process and format the numbers
                            string[] numberArray = numbers.Split(',');
                            for (int i = 0; i < numberArray.Length; i++)
                            {
                                int number = int.Parse(numberArray[i].Trim());
                                string formattedNumber = number < 10 ? "0" + number : number.ToString();
                                numberArray[i] = formattedNumber;
                            }

                            string formattedNumbers = string.Join(",", numberArray);

                            Console.WriteLine($"Date: {date}, Numbers: {formattedNumbers}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No lottery results found on the website.");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error downloading data: {ex.Message}");
            }
        }
    }
}

