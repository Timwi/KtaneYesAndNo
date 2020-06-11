using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class YesAndNoService : MonoBehaviour
{
    public bool QuestionsLoaded = false;

    private string _questionsFile;
    private YesAndNoQuestions _questions;

    public List<YesAndNoQuestions.Question> getQuestions()
    {
        return _questions.questions;
    }

    void Start()
    {
        name = "Yes and No Service";

        _questionsFile = Path.Combine(Path.Combine(Application.persistentDataPath, "Modsettings"), "YesAndNoQuestions.json");

        if (!File.Exists(_questionsFile))
            _questions = new YesAndNoQuestions();
        else
        {
            try
            {
                _questions = JsonConvert.DeserializeObject<YesAndNoQuestions>(File.ReadAllText(_questionsFile), new StringEnumConverter());
                if (_questions == null)
                    throw new Exception("Questions could not be read. Creating new questions...");
                if (_questions.Version < 2)
                {
                    _questions.SiteUrl = YesAndNoQuestions.DefaultSiteUrl;
                    _questions.Version = 2;
                }
                QuestionsLoaded = true;
                Debug.LogFormat(@"[Yes and No Service] Questions file successfully loaded, trying to connect to web...");
            }
            catch (Exception e)
            {
                Debug.LogFormat(@"[Yes and No Service] Error loading questions file:");
                Debug.LogException(e);
                _questions = new YesAndNoQuestions();
            }
        }

        Debug.LogFormat(@"[Yes and No Service] Service is active");
        StartCoroutine(GetData());
    }

    IEnumerator GetData()
    {
        using (var http = UnityWebRequest.Get(_questions.SiteUrl))
        {
            // Request and wait for the desired page.
            yield return http.SendWebRequest();

            if (http.isNetworkError)
            {
                Debug.LogFormat(@"[Yes and No Service] Website {0} responded with error: {1}", _questions.SiteUrl, http.error);
                yield break;
            }

            if (http.responseCode != 200)
            {
                Debug.LogFormat(@"[Yes and No Service] Website {0} responded with code: {1}", _questions.SiteUrl, http.responseCode);
                yield break;
            }

            var allQuestions = JObject.Parse(http.downloadHandler.text)["Questions"] as JArray;
            if (allQuestions == null)
            {
                Debug.LogFormat(@"[Yes and No Service] Website {0} did not respond with a JSON array at “Questions” key.", _questions.SiteUrl, http.responseCode);
                yield break;
            }

            var questions = new List<YesAndNoQuestions.Question>();

            foreach (JObject question in allQuestions)
            {
                var que = question["Question"] as JValue;
                if (que == null || !(que.Value is string))
                    continue;
                var ans = question["Answer"] as JValue;
                if (ans == null || !(ans.Value is string))
                    continue;
                questions.Add(new YesAndNoQuestions.Question { Output = (string)que, Answer = int.Parse((string)ans) });
            }

            Debug.LogFormat(@"[Yes and No Service] Questions successfully loaded from the web, using these for the module:{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, questions.Select(kvp => string.Format("[Yes and No Service] {0} => {1}", kvp.Output, kvp.Answer)).ToArray()));
            _questions.questions = questions;
            QuestionsLoaded = true;

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(_questionsFile)))
                    Directory.CreateDirectory(Path.GetDirectoryName(_questionsFile));
                File.WriteAllText(_questionsFile, JsonConvert.SerializeObject(_questions, Formatting.Indented, new StringEnumConverter()));
                Debug.LogFormat("[Yes and No Service] Successfully saved the questions file.");
            }
            catch (Exception e)
            {
                Debug.LogFormat("[Yes and No Service] Failed to save questions file:");
                Debug.LogException(e);
            }
        }
    }
}