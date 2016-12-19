﻿namespace SpeechToText.Controllers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.Bot.Connector;
    using Services;

    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private readonly MicrosoftCognitiveSpeechService speechService = new MicrosoftCognitiveSpeechService();

        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                string message;

                try
                {
                    var audioAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Equals("audio/wav") || a.ContentType.Equals("application/octet-stream"));
                    if (audioAttachment != null)
                    {
                        var stream = await GetImageStream(connector, audioAttachment);
                        var text = await this.speechService.GetTextFromAudioAsync(stream);
                        message = ProcessText(activity.Text, text);
                    }
                    else
                    {
                        message = "音声ファイルをアップロードしましたか？私は音に反応するので、WAVファイルを送ってみて下さい！";
                    }
                }
                catch (Exception e)
                {
                    message = "あれ、何か問題が起きちゃった。もう一度あとで試してみて！";

                    Trace.TraceError(e.ToString());
                }

                Activity reply = activity.CreateReply(message);
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
            else
            {
                await this.HandleSystemMessage(activity);
            }

            var response = this.Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private static string ProcessText(string input, string text)
        {
            string message = text + "って言ってたね！";

            input = input?.Trim();

            if (!string.IsNullOrEmpty(input))
            {
                var normalizedInput = input.ToUpper();

                if (normalizedInput.Equals("WORD"))
                {
                    var wordCount = text.Split(' ').Count(x => !string.IsNullOrEmpty(x));
                    message += " Word Count: " + wordCount;
                }
                else if (normalizedInput.Equals("CHARACTER"))
                {
                    var characterCount = text.Count(c => c != ' ');
                    message += " Character Count: " + characterCount;
                }
                else if (normalizedInput.Equals("SPACE"))
                {
                    var spaceCount = text.Count(c => c == ' ');
                    message += " Space Count: " + spaceCount;
                }
                else if (normalizedInput.Equals("VOWEL"))
                {
                    var vowelCount = text.ToUpper().Count("AEIOU".Contains);
                    message += " Vowel Count: " + vowelCount;
                }
                else
                {
                    var keywordCount = text.ToUpper().Split(' ').Count(w => w == normalizedInput);
                    message += " Keyword " + input + " found " + keywordCount + " times.";
                }
            }

            return message;
        }

        /// <summary>
        /// Handles the system activity.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <returns>Activity</returns>
        private async Task<Activity> HandleSystemMessage(Activity activity)
        {
            switch (activity.Type)
            {
                case ActivityTypes.DeleteUserData:
                    // Implement user deletion here
                    // If we handle user deletion, return a real message
                    break;
                case ActivityTypes.ConversationUpdate:
                    // Greet the user the first time the bot is added to a conversation.
                    if (activity.MembersAdded.Any(m => m.Id == activity.Recipient.Id))
                    {
                        var connector = new ConnectorClient(new Uri(activity.ServiceUrl));

                        var response = activity.CreateReply();
                        response.Text = "こんにちは！僕は音声をテキスト化するBOTだよ。音声ファイルを送ってくれたら、テキスト化できるだ。WAV形式のファイルを送ってみてね。";

                        await connector.Conversations.ReplyToActivityAsync(response);
                    }

                    break;
                case ActivityTypes.ContactRelationUpdate:
                    // Handle add/remove from contact lists
                    break;
                case ActivityTypes.Typing:
                    // Handle knowing that the user is typing
                    break;
                case ActivityTypes.Ping:
                    break;
            }

            return null;
        }

        private static async Task<Stream> GetImageStream(ConnectorClient connector, Attachment imageAttachment)
        {
            using (var httpClient = new HttpClient())
            {
                // The Skype attachment URLs are secured by JwtToken,
                // you should set the JwtToken of your bot as the authorization header for the GET request your bot initiates to fetch the image.
                // https://github.com/Microsoft/BotBuilder/issues/662
                var uri = new Uri(imageAttachment.ContentUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                }
                else
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(imageAttachment.ContentType));
                }

                return await httpClient.GetStreamAsync(uri);
            }
        }

        /// <summary>
        /// Gets the JwT token of the bot. 
        /// </summary>
        /// <param name="connector"></param>
        /// <returns>JwT token of the bot</returns>
        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }

            return null;
        }

    }
}