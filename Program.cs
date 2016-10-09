using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CoreTweet;
using CoreTweet.Streaming;

namespace ConsoleTwitterStreamer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Console Twitter Streamer");
            Console.WriteLine("You can close this program by pressing Enter key while streaming.");
            Tokens tokens = null;
            while(tokens == null)
            {
                try
                {
                    var consumerKey = "eJWUnMOcfA4zZ1AYTKCmueS94";
                    var consumerSecret = "YLZa7OwqdVnk5w7vnEdoCCWCoGSrbHm1YvCB8SQWs0OOevoKZb";
                    if (System.IO.File.Exists("./Tokens"))
                    {
                        var lines = System.IO.File.ReadAllLines("./Tokens");
                        var accessToken = lines[0];
                        var accessTokenSecret = lines[1];
                        tokens = Tokens.Create(
                            consumerKey,
                            consumerSecret,
                            accessToken,
                            accessTokenSecret);
                    }
                    else
                    {
                        var session = OAuth.Authorize(consumerKey, consumerSecret);
                        Console.WriteLine($"Jump here: {session.AuthorizeUri}");
                        Console.Write("Input PIN here: ");
                        var PIN = Console.ReadLine();
                        tokens = OAuth.GetTokens(session, PIN);
                        string[] accessTokens = {tokens.AccessToken, tokens.AccessTokenSecret};
                        System.IO.File.WriteAllLines("./Tokens", accessTokens);
                    }
                }
                catch (System.Net.WebException we)
                {
                    Console.WriteLine("No connection to the Internet. Press Enter key to close.");
                    Console.Read();
                    Environment.Exit(1);
                }
                catch (TwitterException te)
                {
                    Console.WriteLine("Invaild PIN Code.");
                }
            }
            Console.WriteLine("Succeeded connecting to Twitter!");

            foreach (var pastStatus in tokens.Statuses.HomeTimeline(10).OrderBy(st => st.CreatedAt))
            {
                Console.WriteLine(FormatStatus(pastStatus));
            }

            var s =
                tokens.Streaming.UserAsObservable()
                .Where((StreamingMessage m) => m.Type == MessageType.Create)
                .Cast<StatusMessage>()
                .Select((StatusMessage m) => m.Status);

            s.Catch(
                s.DelaySubscription(TimeSpan.FromSeconds(3))
                    .Retry())
                    .Repeat();

            var stream = s.Subscribe(
                    status => Console.WriteLine(FormatStatus(status)),
                    (Exception ex) => Console.WriteLine(ex),
                    () => Console.WriteLine("終点")
                );

            Thread.Sleep(TimeSpan.FromSeconds(10));
            tokens.Statuses.Update(status => $"Twitter Streamer start up! {DateTime.Now}");

            Console.Read();
            stream.Dispose();
        }

        static string FormatStatus(Status s)
        {
            var formatedStatus = "";
            if (s.RetweetedStatus != null) {
                formatedStatus =
                    $"{s.User.Name}さんが{s.CreatedAt.ToLocalTime().DateTime}にリツイート:\n" +
                    "\n" +
                    FormatStatus(s.RetweetedStatus);
            }
            else if (s.QuotedStatus != null)
            {
                formatedStatus =
                    $"{s.User.Name} @{s.User.ScreenName}\n" +
                    "\n" +
                    $"{s.Text}\n" +
                    $">{new string('-', 100)}\n" +
                    $">{FormatStatus(s.QuotedStatus).Replace("\n", "\n>")}\n" +
                    $"{s.CreatedAt.LocalDateTime}\n" +
                    $"Like:{s.FavoriteCount}\tRT:{s.RetweetCount}\tvia {s.Source}\n" +
                    new string('-', 100);
            }
            else
            {
                formatedStatus =
                    $"{s.User.Name} @{s.User.ScreenName}\n" +
                    "\n" +
                    s.Text + "\n" +
                    "\n" +
                    $"{s.CreatedAt.ToLocalTime().DateTime}\n"+
                    $"Like:{s.FavoriteCount}\tRT:{s.RetweetCount}\tvia {s.Source}\n" +
                    new string('-', 100);
            }
            return formatedStatus;
        }
    }
}
