using System.Collections.Generic;

public sealed class YesAndNoQuestions
{
    public static string DefaultSiteUrl = @"https://ktane.timwi.de/More/YesAndNo/YesAndNoAllQuestions.json";
    public string SiteUrl = @"https://ktane.timwi.de/More/YesAndNo/YesAndNoAllQuestions.json";

    public struct Question
    {
        public string Output;
        public int Answer;
    }

    public List<Question> questions = new List<Question>();

    public int Version = 2;
}