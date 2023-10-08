using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using YoutubeSearch;

namespace YourNamespace
{
    public class Newarticle
    {
        public string title { get; set; }
        public string ArticleContent { get; set; }
        public string ImageUrl { get; set; }
        public string VideoUrl { get; set; }
        public List<string> tags { get; set; }
        public bool succeeded { get; set; }
    }

    public class ChatGPTClient
    {
        private readonly string _apiKey;
        private readonly RestClient _client;

        public ChatGPTClient(string apiKey)
        {
            _apiKey = apiKey;
            _client = new RestClient("https://api.openai.com/v1/engines/text-davinci-003/completions");
        }

        public string SendMessage(string message)
        {
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {_apiKey}");

            var requestBody = new
            {
                prompt = message,
                max_tokens = 1000,
                n = 1,
                stop = (string?)null,
                temperature = 0.7,
            };

            request.AddJsonBody(JsonConvert.SerializeObject(requestBody));

            var response = _client.Execute(request);

            var jsonResponse = JsonConvert.DeserializeObject<dynamic>(response.Content ?? string.Empty);

            return jsonResponse?.choices[0]?.text?.ToString()?.Trim() ?? string.Empty;
        }
    }

    public class Article
    {
        public string Title { get; set; }
        public string Link { get; set; }
        //public string Snippet { get; set; }
    }
    public class AiContentGenerator
    {
        private static readonly string OPENAI_API_KEY = "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";
        private const string GOOGLE_API_KEY = "AIzaSyDX-uzzVnYGQSIuvSncPDVf9jLTvRNLyp0";
        private const string SEARCH_ENGINE_ID = "a7ad4385f2b464df3";

        public static async Task<string> SearchYoutubeVideo(string query, int maxResults = 1)
        {
            return "Empty";
            //Console.WriteLine(query);

            VideoSearch items = new VideoSearch();

            var queryResult = items.SearchQuery(query, maxResults);

            if (queryResult.Count > 0)
            {
                string videoUrl = queryResult[0].Url;
                //Console.WriteLine(videoUrl);
                return videoUrl;
            }
            else
            {
                Console.WriteLine("No videos found.");
                return string.Empty;
            }
        }


        private static async Task<string> GetSearchResultUrl(string description, string searchType)
        {
            //Console.WriteLine(description);
            string query = System.Net.WebUtility.UrlEncode(description);

            string apiUrl = $"https://www.googleapis.com/customsearch/v1?q={query}&key={GOOGLE_API_KEY}&cx={SEARCH_ENGINE_ID}&searchType={searchType}";

            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetAsync(apiUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return null;
                }

                var jsonResponseString = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(jsonResponseString);
                if (jsonResponse["error"] != null)
                {
                    Console.WriteLine($"API Error: {jsonResponse["error"]["message"]}");
                    return null;
                }

                var link = jsonResponse["items"]?.First?["link"]?.ToString();
                //Console.WriteLine(link);
                return link;
            }
        }

        public static async Task<string> ReplaceMediaUrls(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var imgNodes = doc.DocumentNode.SelectNodes("//img[@description]");
            if (imgNodes != null)
            {
                foreach (var img in imgNodes)
                {
                    var description = img.GetAttributeValue("description", null);
                    var alt = img.GetAttributeValue("alt", null);
                    var query = "";
                    if (description == null) query = alt;
                    else query = description;
                    var searchResultUrl = await GetSearchResultUrl(query, "image"); // Set search type to 'image'
                    if (!string.IsNullOrEmpty(searchResultUrl))
                    {
                        img.SetAttributeValue("src", searchResultUrl);
                    }
                }
            }

            var videoNodes = doc.DocumentNode.SelectNodes("//iframe[@description]");
            if (videoNodes != null)
            {
                foreach (var video in videoNodes)
                {
                    var description = video.GetAttributeValue("description", null);
                    var title = video.GetAttributeValue("title", null);
                    var query = "";
                    var searchResultUrl = await SearchYoutubeVideo(query); // Set search type to 'video'
                    if (!string.IsNullOrEmpty(searchResultUrl))
                    {
                        video.SetAttributeValue("src", searchResultUrl);
                    }
                }
            }

            return doc.DocumentNode.OuterHtml;
        }

        public static string getLinkContent(string link)
        {
            try
            {
                var httpClient = new HttpClient();

                var response = httpClient.GetAsync(link).Result;
                var pageContents = response.Content.ReadAsStringAsync().Result;
                var pageDocument = new HtmlDocument();
                pageDocument.LoadHtml(pageContents);
                var pTags = pageDocument.DocumentNode.SelectNodes("//p");
                List<string> texts = new List<string>();

                if (pTags != null)
                {
                    foreach (var pTag in pTags)
                    {
                        texts.Add(Regex.Replace(pTag.InnerText.Trim().Replace("\n", ""), @"\s+", " "));
                    }
                }

                string allText = string.Join(" ", texts);
                return allText;
            }
            catch (Exception ex)
            {
                return "No content";
            }
        }

        public static string RemoveSpecifiedTags(string content)
        {
            content = Regex.Replace(content, @"<img[^>]*>", "", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @"<iframe.*?</iframe>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            content = Regex.Replace(content, @"<h1.*?</h1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            content = Regex.Replace(content, @"<br[^>]*>", "", RegexOptions.IgnoreCase);
            return content;
        }

        public static string RemoveEmptyLines(string content)
        {
            return Regex.Replace(content, @"\r?\n\s*\r?\n", "\n");
        }

        public static string RemoveHtmlTags(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }

        public async static Task<Newarticle> CreateNewArticle(string Language, List<Article> articles, List<string> queries, bool CreateImage, bool CreateVideo)
        {
            string titles = "";
            string contents = "";
            string rawContent = "";
            string sumPrompt = "";
            string sumContent = "";
            var chatGPTClient = new ChatGPTClient(OPENAI_API_KEY);
            int a = 0;


            foreach (var article in articles)
            {
                a++;
                //if (a == 2 || a == 3 || a == 4 || a == 5 || a == 6 || a == 7) continue;
                rawContent = getLinkContent(article.Link);

                if (rawContent.Contains("let us know you're not a robot.") ||
                    rawContent.Contains("enable JS and disable any ad blocker") || rawContent == "No content")
                {
                    continue;
                }
                sumPrompt = $@"Summrize this article as 20 sentences. `{rawContent}`";

                sumContent = chatGPTClient.SendMessage(sumPrompt);

                //Console.WriteLine("\n ------------------------------------------------------------------------------ \n");
                titles += article.Title + "\n";
                contents += sumContent + "\n";
            }

            string prompt;


            prompt = $@"
               Rewrite a comprehensive and engaging article using the provided details, ensuring it's optimized for online readers and search engines.
            
               Content Requirements:
               Keyword Inclusion: Incorporate the keyword into the title and headings.
               Content Structure: Ensure readability, with a minimum limit of 2000 words.
               Visual Content: Include relevant images (with proper copyrights) and videos (either AI-generated or sourced with proper copyrights).
               URL: Maintain a clean, readable, and keyword-rich structure.
               Links: Add valuable internal and external links.
               Instructions:
               Language: en-US
               input 1 image and 1 video tag to relevant position. 
               image tag and video tag have got `descrption` attribute.
               add detailed description concerned with the article to <img> and <iframe> tag as decription attribute. 
               when displaying video, must use <iframe> tag.
               and image and video's width must be 400px.
               Provided Titles:
               {titles}

               Reference Articles:
               {contents}

               Expected Outputs:
               Article: Adapted to WordPress using tags like <h2> and <p>.  don't show `Title:` label. don't show `Tags:` label
               Image: Relevant to the article's theme.
               Video: Relevant, if applicable.
               Tags: Extracted from the article's content. Must involve tags in the last line of the article.
            ";

            Console.Write(prompt);

            string multiLineString = chatGPTClient.SendMessage(prompt);
            var lines = multiLineString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string stringWithoutFirstLine = string.Join("\n", lines.Skip(0));
            string htmlContent = await ReplaceMediaUrls(stringWithoutFirstLine);

            //Console.WriteLine(htmlContent);

            string result = RemoveEmptyLines(htmlContent);

            var titleMatch = Regex.Match(htmlContent, @"<h1>(.*?)</h1>", RegexOptions.Singleline);
            string title = titleMatch.Success ? titleMatch.Groups[1].Value : string.Empty;

            var imgMatch = Regex.Match(htmlContent, @"<img.*?src=[""'](.*?)[""'].*?>", RegexOptions.Singleline);
            string imageURL = imgMatch.Success ? imgMatch.Groups[1].Value : string.Empty;

            var videoMatch = Regex.Match(htmlContent, @"<iframe.*?src=[""'](.*?)[""'].*?>", RegexOptions.Singleline);
            string videoUrl = videoMatch.Success ? videoMatch.Groups[1].Value : string.Empty;

            var tmps = htmlContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (title == string.Empty)
            {
                string temp = tmps[0].Trim();
                if (temp.StartsWith("Title:")) title = temp.Substring(5);
                else title = temp;
            }

            string[] tagsArray = RemoveHtmlTags(tmps[^1].Trim()).Split(',');
            if (tagsArray[0].StartsWith("Tags")) tagsArray[0] = tagsArray[^1].Substring(5);
            List<string> tagsList = new List<string>(tagsArray);
            string content = string.Join("\n", tmps.Skip(1).Take(tmps.Length - 2));
            //Console.Write(content);
            content = RemoveSpecifiedTags(content);
            content = Regex.Replace(content, @"\s+", " ");

            var newArticle = new Newarticle
            {
                title = title,
                ArticleContent = content,
                ImageUrl = imageURL,
                VideoUrl = videoUrl,
                tags = tagsList,
                succeeded = true
            };

            return newArticle;
        }
    }


    class Program
    {
        public static async Task Proc(List<Article> articles)
        {
            //List<Article> articles = new List<Article>
            //{
            //    new Article
            //    {
            //        Title = "Here's why gasoline prices in California are skyrocketing",
            //        Link = "https://abc30.com/heres-why-rising-gas-prices-california-frustrated-drivers/13849945/",
            //        Snippet = "The price of a gallon of gas has risen about 80 cents over the past month. That pinch is being felt by thousands across California."
            //    },
            //    new Article
            //    {
            //        Title = "With gas prices in California soaring, Newsom issues waiver to provide financial relief at the pump",
            //        Link = "https://www.sandiegouniontribune.com/business/story/2023-09-28/with-gas-prices-in-california-soaring-newsom-issues-waiver-to-provide-financial-relief-at-the-pump",
            //        Snippet = "In an attempt to curb a recent spike in gasoline prices, Gov. Gavin Newsom late Thursday instructed California regulators to speed the..."
            //    },
            //    //new Article
            //    //{
            //    //    Title =  "Drivers react to possible relief at the pump following Newsom letter",
            //    //    Link = "https://www.10news.com/news/local-news/drivers-react-to-possible-relief-at-the-pump-following-newsom-letter",
            //    //    Snippet = "This week, Governor Newsom sent a letter calling for refiners to pump out the less expensive winter blend gas ahead of schedule.",
            //    //},
            //    //new Article
            //    //{
            //    //    Title = "Oil Prices on a March Toward $100 a Barrel",
            //    //    Link = "https://www.nytimes.com/2023/09/27/business/oil-price-100-barrel.html",
            //    //    Snippet = "Analysts have raised their forecasts for oil prices, as they try to understand Saudi Arabia's intentions with recent production cuts.",
            //    //},
            //    //new Article
            //    //{
            //    //    Title = "California Gasoline Tops $6 as Governor Newsom Lifts Smog Rule for Relief",
            //    //    Link = "https://www.bloomberg.com/news/articles/2023-09-28/california-gasoline-tops-6-as-newsom-directs-smog-rule-waiver-ln3rygcz",
            //    //    Snippet = "Summer is over, but gasoline prices are heating up in California, prompting Governor Gavin Newsom to lift an anti-smog rule for relief at...",
            //    //},
            //    //new Article
            //    //{
            //    //    Title = "Immediate relief is needed.’ California GOP lawmakers call for suspension of gas tax",
            //    //    Link = "https://www.sacbee.com/news/politics-government/capitol-alert/article279882984.html",
            //    //    Snippet = "California Gov. Gavin Newsom should call a special session of the Legislature in order to suspend the state's gas tax, Republican lawmakers...",
            //    //},
            //    //new Article
            //    //{
            //    //    Title = "California gas prices continue to rise; expert explains the causes",
            //    //    Link = "https://krcrtv.com/news/local/california-gas-prices-continue-to-rise-expert-explains-the-causes",
            //    //    Snippet = "NORTHSTATE, Calif. — Gas prices continue to climb across California. The state average for gas is now above $6, according to AAA; that's the...",
            //    //},
            //};

            List<string> queries = new List<string> { "query1", "query2" };

            var result = await AiContentGenerator.CreateNewArticle("English", articles, queries, true, false);

            Console.WriteLine($"Title: {result.title}");
            Console.WriteLine($"Content: {result.ArticleContent}");
            Console.WriteLine($"Image URL: {result.ImageUrl}");
            Console.WriteLine($"Video URL: {result.VideoUrl}");
            Console.WriteLine($"Tags: {string.Join(", ", result.tags)}");
            Console.WriteLine($"Succeeded: {result.succeeded}");

        }
        public static async Task Main(string[] args)
        {
            Console.WriteLine("How many names do you want to input?");

            List<Article> articles = new List<Article>();

            // Read the count of names
            int count;
            while (!int.TryParse(Console.ReadLine(), out count) || count <= 0)
            {
                Console.WriteLine("Invalid input. Please enter a positive integer.");
            }

            for (int i = 0; i < count; i++)
            {
                Console.WriteLine($"Enter details for article #{i + 1}:");

                Article article = new Article();

                Console.WriteLine("Enter the title of the article:");
                article.Title = Console.ReadLine();

                Console.WriteLine("Enter the link of the article:");
                article.Link = Console.ReadLine();

                //Console.WriteLine("Enter the snippet of the article:");
                //article.Snippet = Console.ReadLine();

                articles.Add(article);

                Console.WriteLine(); // Just for better readability.
            }

            DisplayArticlesDetails(articles);
            Proc(articles);
        }

        public static void DisplayArticlesDetails(List<Article> articles)
        {
            Console.WriteLine("\nArticles Details:");

            int counter = 1;
            foreach (var article in articles)
            {
                Console.WriteLine($"\nArticle #{counter}:");
                Console.WriteLine($"Title: {article.Title}");
                Console.WriteLine($"Link: {article.Link}");
                //Console.WriteLine($"Snippet: {article.Snippet}");
                counter++;
            }
        }
    }
}