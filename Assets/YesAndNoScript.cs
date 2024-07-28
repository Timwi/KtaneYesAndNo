using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class YesAndNoScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMRuleSeedable RuleSeed;

    public KMSelectable leftButton;
    public KMSelectable rightButton;
    public KMSelectable resetButton;
    public TextMesh question;
    public TextMesh progressText;

    struct Colors
    {
        public string ColorName;
        public Color32 Color;
    }

    struct Condition
    {
        public string Explanation;
        public Func<GameObject, bool> Cond;
    }

    struct Switch
    {
        public string Explanation;
        public Func<GameObject, bool> Cond;
        public int SwitchValue;
    }

    private static readonly Colors[] color = new Colors[]
    {
        new Colors { ColorName = "blue", Color = new Color32(20, 16, 213, 255) },
        new Colors { ColorName = "green", Color = new Color32(32, 243, 70, 255) },
        new Colors { ColorName = "yellow", Color = new Color32(216, 216, 32, 255) },
        new Colors { ColorName = "magenta", Color = new Color32(219, 42, 238, 255) },
        new Colors { ColorName = "orange", Color = new Color32(255, 164, 0, 255) },
        new Colors { ColorName = "red", Color = new Color32(243, 32, 32, 255) }
    };

    private static Switch[] leftSwitch;
    private static Switch[] rightSwitch;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private int currentQuestion = 0;
    private int timesLeft = 0;
    private int timesRight = 0;
    private bool switchActive = false;
    private List<int> questionList;
    private List<YesAndNoQuestions.Question> questions;
    private MonoRandom rnd;
    private bool resetDone = false;
    private Coroutine resetActive;
    private bool catastrophicProblem;

    void Awake()
    {
        moduleId = moduleIdCounter++;

        leftButton.OnInteract += delegate
        {
            if (moduleSolved)
                return false;
            leftButton.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, leftButton.transform);
            Press(left: true);
            return false;
        };

        rightButton.OnInteract += delegate
        {
            if (moduleSolved)
                return false;
            rightButton.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightButton.transform);
            Press(left: false);
            return false;
        };

        resetButton.OnInteract += delegate
        {
            if (moduleSolved)
                return false;
            if (resetActive != null)
                StopCoroutine(resetActive);
            rightButton.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightButton.transform);
            resetActive = StartCoroutine(Reset());
            return false;
        };

        resetButton.OnInteractEnded += delegate
        {
            if (moduleSolved)
                goto skip;
            if (!resetDone)
            {
                question.text = questions[questionList[currentQuestion]].Output;
                StopCoroutine(resetActive);
            }
            else
            {
                resetDone = false;
                StopCoroutine(resetActive);
                timesLeft = 0;
                timesRight = 0;
                switchActive = false;
                currentQuestion = 0;
                question.text = questions[questionList[currentQuestion]].Output;
                question.color = new Color32(255, 255, 255, 255);
                Debug.LogFormat(@"[Yes and No #{0}] Module was reset", moduleId);
            }
            skip:;
        };
    }

    private void Press(bool left)
    {
        if (catastrophicProblem)
        {
            GetComponent<KMBombModule>().HandlePass();
            moduleSolved = true;
            return;
        }

        if (questionList == null)   // Questions have not loaded yet
            return;

        if (left ^ switchActive ^ (leftButton.GetComponentInChildren<TextMesh>().text == "YES") ^ (questions[questionList[currentQuestion]].Answer == 0))
        {
            Debug.LogFormat(@"[Yes and No #{0}] Question: “{3}” — you pressed {1} when I expected {2}. Strike!", moduleId, left ? "left" : "right", left ? "right" : "left", questions[questionList[currentQuestion]].Output);
            GetComponent<KMBombModule>().HandleStrike();
            timesLeft = 0;
            timesRight = 0;
            switchActive = false;
            Debug.LogFormat(@"[Yes and No #{0}] Press counters and switch have been reset.", moduleId);
        }
        else
        {
            if (left)
                timesLeft++;
            else
                timesRight++;

            Debug.LogFormat(@"[Yes and No #{0}] Question: “{2}” — pressing {1} was correct.", moduleId, left ? "left" : "right", questions[questionList[currentQuestion]].Output);

            if (timesLeft == leftSwitch.First(eva => eva.Cond(leftButton.gameObject)).SwitchValue)
            {
                Debug.LogFormat(@"[Yes and No #{0}] Left switch occurred after {1} button presses. Resetting left button counter.", moduleId, timesLeft);
                switchActive = !switchActive;
                timesLeft = 0;
            }
            if (timesRight == rightSwitch.First(eva => eva.Cond(rightButton.gameObject)).SwitchValue)
            {
                Debug.LogFormat(@"[Yes and No #{0}] Right switch occurred after {1} button presses. Resetting right button counter.", moduleId, timesRight);
                switchActive = !switchActive;
                timesRight = 0;
            }

            currentQuestion++;

            if (currentQuestion == questionList.Count)
            {
                Debug.LogFormat(@"[Yes and No #{0}] Module solved.", moduleId);
                question.text = "Solved";
                question.color = color.First(cl => cl.ColorName == "green").Color;
                moduleSolved = true;
                GetComponent<KMBombModule>().HandlePass();
            }
            else
                question.text = questions[questionList[currentQuestion]].Output;

            progressText.text = string.Format("{0}<color=#808080>{1}</color>", new string('•', currentQuestion), new string('•', questionList.Count - currentQuestion));
        }
    }

    private Switch[] GetRuleSeedForButton(MonoRandom rnd)
    {
        var colors = new List<string> { "red", "orange", "yellow", "green", "blue", "magenta" };
        var rules = new List<Switch>();

        var makeSingleColorRule = new Action(() =>
        {
            // Rule with a single color
            var c = rnd.Next(0, colors.Count);
            var colorName = colors[c];
            colors.RemoveAt(c);
            rules.Add(new Switch
            {
                Explanation = string.Format("The button is {0}", colorName),
                Cond = button => button.GetComponent<MeshRenderer>().material.color == color.First(cl => cl.ColorName == colorName).Color
            });
        });

        var makeTwoColorRule = new Action(() =>
        {
            // Rule with two possible colors
            var c1 = rnd.Next(0, colors.Count);
            var color1 = colors[c1];
            colors.RemoveAt(c1);
            var c2 = rnd.Next(0, colors.Count);
            var color2 = colors[c2];
            colors.RemoveAt(c2);
            rules.Add(new Switch
            {
                Explanation = string.Format("The button is either {0} or {1}", color1, color2),
                Cond = button => button.GetComponent<MeshRenderer>().material.color == color.First(cl => cl.ColorName == color1).Color
                    || button.GetComponent<MeshRenderer>().material.color == color.First(cl => cl.ColorName == color2).Color
            });
        });

        makeSingleColorRule();
        makeTwoColorRule();
        if (rnd.Next(0, 2) != 0)
            makeSingleColorRule();
        else
            makeTwoColorRule();

        // Insert yes/no label rule
        var label = rnd.Next(0, 2) != 0 ? "YES" : "NO";
        var negated = rnd.Next(0, 2) != 0;
        rules.Insert(rnd.Next(0, rules.Count), new Switch
        {
            Explanation = string.Format("The button {0} ‘{1}’", negated ? "is not labeled" : "is labeled", label),
            Cond = button => negated
                ? button.GetComponentInChildren<TextMesh>().text != label
                : button.GetComponentInChildren<TextMesh>().text == label
        });

        var numbers = new List<int> { 1, 2, 3, 4 };
        rnd.ShuffleFisherYates(numbers);
        numbers.Insert(rnd.Next(0, numbers.Count), rnd.Next(1, 5));

        rules.Add(new Switch { Explanation = "Otherwise", Cond = button => true });
        var rulesArr = rules.ToArray();

        for (var i = 0; i < 5; i++)
            rulesArr[i].SwitchValue = numbers[i];
        return rulesArr;
    }

    void Start()
    {
        rnd = RuleSeed.GetRNG();
        leftSwitch = GetRuleSeedForButton(rnd);
        rightSwitch = GetRuleSeedForButton(rnd);

        MixButtons();
        StartCoroutine(Game());
    }

    void MixButtons()
    {
        var cl = Enumerable.Range(0, 6).ToList();
        cl.Shuffle();

        leftButton.GetComponent<MeshRenderer>().material.color = color[cl[0]].Color;
        rightButton.GetComponent<MeshRenderer>().material.color = color[cl[1]].Color;
        leftButton.GetComponentInChildren<TextMesh>().text = cl[0] <= 3 ? "YES" : "NO";
        rightButton.GetComponentInChildren<TextMesh>().text = cl[0] > 3 ? "YES" : "NO";
        leftButton.GetComponentInChildren<TextMesh>().color = cl[0] <= 3 ? color.First(col => col.ColorName == "green").Color : color.First(col => col.ColorName == "red").Color;
        rightButton.GetComponentInChildren<TextMesh>().color = cl[0] > 3 ? color.First(col => col.ColorName == "green").Color : color.First(col => col.ColorName == "red").Color;
    }

    IEnumerator Game()
    {
        var yanService = FindObjectOfType<YesAndNoService>();

        if (yanService == null)
        {
            Debug.LogFormat(@"[Yes and No #{0}] Catastrophic problem: Yes and No Service is not present.", moduleId);
            question.text = "Press anything to solve";
            catastrophicProblem = true;
            yield break;
        }

        yield return new WaitUntil(() => yanService.QuestionsLoaded);

        questions = yanService.getQuestions();
        if (questions == null)
        {
            Debug.LogFormat(@"[Yes and No #{0}] Catastrophic problem: Yes and No Service did not respond with any questions. Please contact Timwi on Discord.", moduleId);
            question.text = "Press anything to solve";
            catastrophicProblem = true;
            yield break;
        }

        var gameLength = Random.Range(5, questions.Count > 15 ? 15 : questions.Count);
        var allQuestion = Enumerable.Range(0, questions.Count).ToList();
        allQuestion.Shuffle();
        questionList = allQuestion.Take(gameLength).ToList();
        Debug.LogFormat(@"[Yes and No #{0}] ----------MODULE SETUP----------", moduleId);
        Debug.LogFormat(@"[Yes and No #{0}] Using ruleseed: {1}", moduleId, rnd.Seed);
        Debug.LogFormat(@"[Yes and No #{0}] ----------Left Switch Conditions----------", moduleId, rnd.Seed);
        for (int i = 0; i < leftSwitch.Length; i++)
        {
            Debug.LogFormat(@"[Yes and No #{0}] Left switch condition {1}: {2} - Value: {3}", moduleId, (i + 1).ToString(), leftSwitch[i].Explanation, leftSwitch[i].SwitchValue.ToString());
        }
        Debug.LogFormat(@"[Yes and No #{0}] ----------Right Switch Conditions----------", moduleId, rnd.Seed);
        for (int i = 0; i < leftSwitch.Length; i++)
        {
            Debug.LogFormat(@"[Yes and No #{0}] Right switch condition {1}: {2} - Value: {3}", moduleId, (i + 1).ToString(), rightSwitch[i].Explanation, rightSwitch[i].SwitchValue.ToString());
        }
        Debug.LogFormat(@"[Yes and No #{0}] ----------Questions and Answers----------", moduleId, rnd.Seed);
        Debug.LogFormat(@"[Yes and No #{0}] There will be a total of {1} {2}", moduleId, gameLength.ToString(), gameLength == 1 ? "question" : "questions");
        for (int i = 0; i < questions.Count; i++)
        {
            Debug.LogFormat(@"[Yes and No #{0}] Nr. {1} - Question: '{2}' - Answer: '{3}'", moduleId, (i + 1).ToString(), questions[i].Output, questions[i].Answer == 1 ? "Yes" : "No");
        }
        Debug.LogFormat(@"[Yes and No #{0}] ----------Selected Conditions----------", moduleId, rnd.Seed);
        Debug.LogFormat(@"[Yes and No #{0}] Left switch will occur after {1} button presses. Reason: {2}", moduleId, leftSwitch.First(eva => eva.Cond(leftButton.gameObject)).SwitchValue, leftSwitch.First(eva => eva.Cond(leftButton.gameObject)).Explanation);
        Debug.LogFormat(@"[Yes and No #{0}] Right switch will occur after {1} button presses. Reason: {2}", moduleId, rightSwitch.First(eva => eva.Cond(rightButton.gameObject)).SwitchValue, rightSwitch.First(eva => eva.Cond(rightButton.gameObject)).Explanation);
        Debug.LogFormat(@"[Yes and No #{0}] ----------DEFUSER INPUT----------", moduleId);

        question.text = questions[questionList[0]].Output;
        progressText.text = string.Format("<color=#808080>{0}</color>", new string('•', questionList.Count));
    }

    IEnumerator Reset()
    {
        var resetTime = 0;
        var resetText = "Resetting module...";

        while (resetTime < 2)
        {
            question.text = "";
            for (int i = 0; i < resetText.Length; i++)
            {
                question.text += resetText[i];
                yield return new WaitForSeconds(.1f);
            }
            resetTime++;
        }
        yield return null;
        question.text = "Reset done";
        question.color = color.First(cl => cl.ColorName == "green").Color;
        progressText.text = string.Format("<color=#808080>{0}</color>", new string('•', questionList.Count));
        resetDone = true;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} yes/no/left/right/L/R/Y/N/reset [press a button]";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (moduleSolved)
        {
            yield return "sendtochaterror The module is already solved.";
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*(yes|Y)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (leftButton.GetComponentInChildren<TextMesh>().text == "YES")
                leftButton.OnInteract();
            else
                rightButton.OnInteract();
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*(no|N)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (leftButton.GetComponentInChildren<TextMesh>().text == "NO")
                leftButton.OnInteract();
            else
                rightButton.OnInteract();
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*(left|L)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            leftButton.OnInteract();
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*(right|R)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            rightButton.OnInteract();
            yield break;
        }
        else if (Regex.IsMatch(command, @"^\s*(reset)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            resetButton.OnInteract();
            yield return new WaitUntil(() => resetDone);
            resetButton.OnInteractEnded();
            yield break;
        }
        else
        {
            yield return "sendtochaterror Invalid Command.";
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat(@"[Yes and No #{0}] Module was force solved by TP.", moduleId);

        var yesButton = leftButton.GetComponentInChildren<TextMesh>().text == "YES" ? leftButton : rightButton;
        var noButton = leftButton.GetComponentInChildren<TextMesh>().text == "NO" ? leftButton : rightButton;

        while (!moduleSolved)
        {
            if (moduleSolved)
                yield break;

            if (questions[questionList[currentQuestion]].Answer == 0)
            {
                if (!switchActive)
                    noButton.OnInteract();
                else
                    yesButton.OnInteract();
            }

            else
            {
                if (!switchActive)
                    yesButton.OnInteract();
                else
                    noButton.OnInteract();
            }
            yield return new WaitForSeconds(0.1f);
        }
    }


}

