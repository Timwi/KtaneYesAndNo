using System.Collections.Generic;

public sealed class YesAndNoQuestions
{
    public string SiteUrl = @"http://forler.ddns.net:8000/YesAndNoAllQuestions.json";

    public struct Question
    {
        public string Output;
        public int Answer;
    }

    public List<Question> questions = new List<Question>();

    public int Version = 1;
}