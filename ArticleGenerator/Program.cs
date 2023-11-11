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
        private static readonly string OPENAI_API_KEY = "";
        private const string GOOGLE_API_KEY = "";
        private const string SEARCH_ENGINE_ID = "";

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
               Title and Headings
Craft a title that naturally incorporates the keyword, ensuring it is captivating and informative. Structure your headings (using <h2>, <h3>, etc.) to also include the keyword where relevant, which will contribute to better search engine visibility.

Content Structure
The article should be at least 2000 words to meet the minimum word count requirement. Break down the content into manageable sections with subheadings to enhance readability. Utilize short paragraphs, bullet points, and numbered lists where applicable.

Using the title and related articles, the article will be adapted to wordpress with wordpress tags like this structure\r\nwp:paragraph <!-- wp:heading-- >\r\n                < h2 class=\"\"wp-block-heading\"\">T header</h2>\r\n                <!-- /wp:heading -->\r\n\r\n                <!-- wp:paragraph -->\r\n                <p> content</p>\r\n                <!-- /wp:paragraph →


Visual Content
Incorporate relevant images and videos that complement the text. For images, use the <img> tag with a description attribute providing a detailed caption. For videos, embed them using the <iframe> tag with the same approach to descriptions. Ensure both elements are set to a width of 400px to maintain a consistent layout.

URL Structure
Ensure the article's URL is clean, readable, and contains the primary keyword. This will help with search engine ranking and user experience.

Links
Strategically place internal links to other pages on your site and external links to authoritative sources. This adds value for readers and can improve SEO.

Language and Coding
The article should be written in American English (en-US). When adapting the content for WordPress, use tags like <h2> for subheadings and <p> for paragraphs to format the text properly.

Provided Titles and Reference Articles
Use the provided titles and reference articles as a foundation for your piece. Draw inspiration from them to ensure the content is unique and engaging.

Tags
Extract tags from the content that are relevant to the article's theme. Place these tags at the end of the article, signaling their importance for search visibility.

Image and Video Tags Example:
For images:

html
Copy code
<img src="URL_to_image.jpg" width="400px" description="Detailed description of the image related to the article's theme." />
For videos:

html
Copy code
<iframe src="URL_to_video" width="400px" description="Detailed description of the video content, ensuring relevance to the article's theme." ></iframe>
Expected Outputs
Finally, generate an article that adheres to WordPress 

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
            //    //    Title = "Immediate relief is needed.� California GOP lawmakers call for suspension of gas tax",
            //    //    Link = "https://www.sacbee.com/news/politics-government/capitol-alert/article279882984.html",
            //    //    Snippet = "California Gov. Gavin Newsom should call a special session of the Legislature in order to suspend the state's gas tax, Republican lawmakers...",
            //    //},
            //    //new Article
            //    //{
            //    //    Title = "California gas prices continue to rise; expert explains the causes",
            //    //    Link = "https://krcrtv.com/news/local/california-gas-prices-continue-to-rise-expert-explains-the-causes",
            //    //    Snippet = "NORTHSTATE, Calif. � Gas prices continue to climb across California. The state average for gas is now above $6, according to AAA; that's the...",
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
