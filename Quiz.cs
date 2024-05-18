using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using QuizApi;

namespace Quiz
{
    public class Quiz : BasePlugin
    {
        public override string ModuleName => "Quiz";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.1";

        private readonly PluginCapability<IQuizApi> _pluginCapability = new("quiz:core");

        private static string? ChatPrefix;
        public QuizApi? _api;
        private static readonly Config _cfg = Config.LoadConfig();
        private static int _core;
        public Dictionary<CCSPlayerController, Action<string>> NextCommandAction { get; set; } = [];
        private readonly Dictionary<CCSPlayerController, int> playerAttempts = [];
        private bool _quiz = false;
        private float _Time;
        private CounterStrikeSharp.API.Modules.Timers.Timer? _QuizTimer;
        private float _TimeEnd;
        private int _Min;
        private int _Max;
        private Question? CurrentQuestion { get; set; }
        private string? CurrentAnswer { get; set; }
        private static readonly char[] separator = [';'];

        public override void Load(bool hotReload)
        {
            _api = new QuizApi(this);
            Capabilities.RegisterPluginCapability(_pluginCapability, () => _api);

            AddCommandListener("say", OnSay);
            AddCommandListener("say_team", OnSay);

            _api.NextCommandAction = [];

            SetupEventHandlers();
            SetupPrefix();

            QuestionCfg();

            _api.ModuleRegistered += OnModuleRegistered;
        }

        private void SetupEventHandlers()
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
        }

        private void OnMapStart(string mapName)
        {
            _quiz = false;
            MainCfg();
            return;
        }

        private void QuestionCfg()
        {
            Config.LoadQuestions();
            string configFilePath = Config.GetQuestionConfigFilePath();

            if (File.Exists(configFilePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(configFilePath);
                    var questionsConfig = JsonConvert.DeserializeObject<Dictionary<string, Question>>(jsonContent);

                    if (questionsConfig != null)
                    {
                        _cfg.Questions = questionsConfig;
                        Logger.LogInformation($"Loaded {questionsConfig.Count} questions.");
                    }
                    else
                    {
                        Logger.LogError("No questions found in the configuration file.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error reading questions configuration: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"Questions configuration file not found ({configFilePath}).");
            }
        }

        private void MainCfg()
        {
            _Time = _cfg.Time;
            _QuizTimer = AddTimer(_Time, () => TimerQuiz());
            _TimeEnd = _cfg.TimeEnd;

            _Min = _cfg.Min;
            _Max = _cfg.Max;
        }

        private static int GetMaxQuestion()
        {
            return _cfg.Questions.Count;
        }

        private Question? GetRandomQuestion()
        {
            int maxQuestions = GetMaxQuestion();
            if (maxQuestions == 0)
            {
                Logger.LogError("No questions available.");
                return null;
            }

            int questionIndex = new Random().Next(0, maxQuestions);
            return _cfg.Questions.ElementAt(questionIndex).Value;
        }

        private void TimerQuiz()
        {
            if (!_quiz)
            {
                StartQuiz();
                return;
            }

            ChooseQuestionType();

            _QuizTimer?.Kill();
            _QuizTimer = AddTimer(_TimeEnd, TimerQuizEnd);
        }

        private void StartQuiz()
        {
            if (!_quiz)
            {
                _quiz = true;
                if (ChatPrefix != null)
                {
                    BroadcastMessage($"{Localizer["StartQuiz", ChatPrefix]}");
                }
                TimerQuiz();
            }
        }

        private void TimerQuizEnd()
        {
            if (_quiz)
            {
                if (_cfg.DisplayAnswer && CurrentAnswer != null && ChatPrefix != null)
                {
                    BroadcastMessage($"{Localizer["CurrentAnswer", ChatPrefix, CurrentAnswer]}");
                }
                else
                {
                    if (ChatPrefix != null)
                    {
                        BroadcastMessage($"{Localizer["LuckNextTime", ChatPrefix]}");
                    }
                }
                _api?.StopQuiz();
                ResetQuizForNextRound();
            }
        }

        private void ResetQuizForNextRound()
        {
            CurrentQuestion = null;
            CurrentAnswer = null;
            playerAttempts.Clear();
            _quiz = false;

            if (_QuizTimer != null)
            {
                _QuizTimer.Kill();
                _QuizTimer = null;
            }

            _QuizTimer = AddTimer(_Time, StartQuiz);
        }

        private void ChooseQuestionType()
        {
            List<Action> possibleActions = [];
            if (_cfg.UseQuestions) possibleActions.Add(AskQuestion);
            if (_cfg.UseExamples) possibleActions.Add(GenerateExample);
            if (_cfg.UseRandomNumbers) possibleActions.Add(AskRandomNumber);

            if (possibleActions.Count != 0)
            {
                var actionToExecute = possibleActions[new Random().Next(possibleActions.Count)];
                actionToExecute();
                _api?.StartQuiz(possibleActions.Count);
            }
        }

        private void AskQuestion()
        {
            var question = GetRandomQuestion();
            if (question != null && ChatPrefix != null && question.QuestionText != null && _cfg.AnswerTag != null)
            {
                CurrentQuestion = question;
                CurrentAnswer = question.Answer;
                BroadcastMessage($"{Localizer["Question", ChatPrefix, question.QuestionText]}");
                BroadcastMessage($"{Localizer["Example", ChatPrefix, _cfg.AnswerTag]}");
            }
        }

        private void AskRandomNumber()
        {
            int randomNumber = new Random().Next(_Min, _Max);
            CurrentAnswer = randomNumber.ToString();
            if (ChatPrefix != null && _cfg.AnswerTag != null)
            {
                BroadcastMessage($"{Localizer["RandomNumber", ChatPrefix, _Min, _Max]}");
                BroadcastMessage($"{Localizer["Example", ChatPrefix, _cfg.AnswerTag]}");
            }
        }

        private void GenerateExample()
        {
            (int a, int b) = GetRandomNumbers(1, 101);

            while (a == 0 || b == 0)
            {
                (a, b) = GetRandomNumbers(1, 101);
            }

            (int result, string example) = PerformOperation(a, b);

            CurrentAnswer = result.ToString();

            if (ChatPrefix != null && _cfg?.AnswerTag != null)
            {
                string mathMessage = Localizer["Math", ChatPrefix, example];
                string exampleMessage = Localizer["Example", ChatPrefix, _cfg.AnswerTag];

                BroadcastMessage(mathMessage);
                BroadcastMessage(exampleMessage);
            }
        }

        private (int, int) GetRandomNumbers(int min, int max)
        {
            Random random = new();
            int a = random.Next(min, max + 1);
            int b = random.Next(min, max + 1);

            while (a == 0 || b == 0)
            {
                a = random.Next(min, max + 1);
                b = random.Next(min, max + 1);
            }

            return (a, b);
        }

        private static (int, string) PerformOperation(int a, int b)
        {
            int operationType = GetRandomOperation(4);
            int result;
            string example;

            switch (operationType)
            {
                case 1:
                    result = a + b;
                    example = $"{a} + {b}";
                    break;
                case 2:
                    while (a < b)
                    {
                        a = GetRandomNumber(1, b);
                    }
                    result = a - b;
                    example = $"{a} - {b}";
                    break;
                case 3:
                    result = a * b;
                    example = $"{a} * {b}";
                    break;
                case 4:
                    while (b == 0 || a % b != 0)
                    {
                        b = GetRandomNumber(1, 100);
                    }
                    result = a / b;
                    example = $"{a} / {b}";
                    break;
                default:
                    throw new InvalidOperationException("Unknown operation type encountered in GenerateExample.");
            }

            return (result, example);
        }

        private static int GetRandomOperation(int max)
        {
            Random random = new();
            return random.Next(1, max + 1);
        }

        private static int GetRandomNumber(int min, int max)
        {
            Random random = new();
            return random.Next(min, max + 1);
        }

        public void NextQuiz()
        {
            if (ChatPrefix != null)
            {
                _quiz = true;
                BroadcastMessage($"{Localizer["NextQuiz", ChatPrefix]}");
                TimerQuiz();
            }
        }

        private static void BroadcastMessage(string message)
        {
            foreach (var player in GetPlayers())
            {
                player.PrintToChat(message);
            }
        }

        private static IEnumerable<CCSPlayerController> GetPlayers()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot);
        }

        private void SetupPrefix()
        {
            ChatPrefix = $"{Localizer["Prefix"]}";
        }

        private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot || !_quiz) return HookResult.Continue;

            string message = info.GetArg(1).Trim();

            if (!message.StartsWith($"{_cfg.AnswerTag}"))
            {
                return HookResult.Continue;
            }

            message = message[1..].Trim();

            if (CurrentAnswer != null && ChatPrefix != null)
            {
                if (!playerAttempts.TryGetValue(player, out int attempts))
                {
                    playerAttempts[player] = 0;
                }

                if (attempts < _cfg.MaxAttempts)
                {
                    playerAttempts[player]++;
                    IEnumerable<string> answers = CurrentAnswer.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                                               .Select(a => a.Trim());

                    if (answers.Any(answer => answer.Equals(message, StringComparison.OrdinalIgnoreCase)))
                    {
                        _api?.TriggerPlayerWin(player);
                        BroadcastMessage($"{Localizer["CorrectAnswer", ChatPrefix, player.PlayerName, CurrentAnswer]}");
                        ResetQuizForNextRound();
                        return HookResult.Stop;
                    }
                    else
                    {
                        if (playerAttempts[player] >= _cfg.MaxAttempts)
                        {
                            player.PrintToChat($"{Localizer["NoMoreAttempts", ChatPrefix]}");
                            return HookResult.Stop;
                        }
                        else
                        {
                            player.PrintToChat($"{Localizer["AttemptsLeft", ChatPrefix, _cfg.MaxAttempts - playerAttempts[player]]}");
                            return HookResult.Stop;
                        }
                    }
                }
                else
                {
                    player.PrintToChat($"{Localizer["NoMoreAttempts", ChatPrefix]}");
                    return HookResult.Stop;
                }
            }

            return HookResult.Continue;
        }

        private void OnModuleRegistered()
        {
            if (_api != null)
            {
                _core = _api.HasRegisteredModules() ? 1 : 0;

                if (_core == 0)
                {
                    Logger.LogInformation("Module not found. Core is waiting for a module");
                }
                else
                {
                    Logger.LogInformation("Quiz module successfully detected, core fully active.");
                }
            }
        }

        public class QuizApi(Quiz QuizCore) : IQuizApi
        {
            public string Quiz { get; } = QuizCore.ModuleName;

            public event Action? ModuleRegistered;
            public event Action<CCSPlayerController>? OnPlayerWin;
            public event Action? OnQuizStart;
            public event Action? OnQuizEnd;
            public event Action<CCSPlayerController>? OnClientAnswered;
            public event Action<CCSPlayerController>? OnPlayerLose;

            private readonly List<Action<CCSPlayerController, CCSPlayerController, string>> _modules = [];
            public Dictionary<CCSPlayerController, Action<string>>? NextCommandAction { get; set; } = [];

            private int quizValue;
            private string currentAnswer = string.Empty;

            public void RegisterNextCommandAction(CCSPlayerController player, Action<string> action)
            {
                if (NextCommandAction != null)
                {
                    if (!NextCommandAction.TryAdd(player, action))
                    {
                        NextCommandAction[player] = action;
                    }
                }
            }

            public void RegisterModule(Action<CCSPlayerController, CCSPlayerController, string> action)
            {
                if (!_modules.Contains(action))
                {
                    _modules.Add(action);
                    ModuleRegistered?.Invoke();
                }
            }

            public bool HasRegisteredModules() => _modules.Count > 0;

            public void StartQuiz(int quizMode)
            {
                quizValue = quizMode;
                OnQuizStart?.Invoke();
            }

            public void StopQuiz()
            {
                OnQuizEnd?.Invoke();
            }

            public int GetQuizValue() => quizValue;

            public string GetAnswer() => currentAnswer;

            public void SetAnswer(string answer) => currentAnswer = answer;

            public void TriggerPlayerWin(CCSPlayerController player)
            {
                OnPlayerWin?.Invoke(player);
            }

            public void TriggerPlayerLose(CCSPlayerController player)
            {
                OnPlayerLose?.Invoke(player);
            }

            public void TriggerStartQuiz() { }

            public void HandleClientAnswer(CCSPlayerController player, string answer)
            {
                OnClientAnswered?.Invoke(player);
                if (answer.Equals(currentAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    TriggerPlayerWin(player);
                }
                else
                {
                    TriggerPlayerLose(player);
                }
            }

            public string GetTranslatedText(string name, params object[] args) => QuizCore.Localizer[name, args];
        }
    }

    public class Config
    {
        public bool UseQuestions { get; set; } = true;
        public bool UseExamples { get; set; } = true;
        public bool UseRandomNumbers { get; set; } = true;

        public float Time { get; set; } = 30.0f;
        public float TimeEnd { get; set; } = 15.0f;

        public int Min { get; set; } = 1;
        public int Max { get; set; } = 100;

        public bool DisplayAnswer { get; set; } = true;
        public Dictionary<string, Question> Questions { get; set; } = [];
        public int MaxAttempts { get; set; } = 3;
        public string? AnswerTag { get; set; } = "/";

        public static Config LoadConfig()
        {
            string configFilePath = GetConfigFilePath();

            if (!File.Exists(configFilePath))
            {
                Config defaultConfig = new()
                {
                    UseQuestions = true,
                    UseExamples = true,
                    UseRandomNumbers = true,
                    Time = 30.0f,
                    TimeEnd = 15.0f,
                    Min = 1,
                    Max = 100,
                    DisplayAnswer = true,
                    MaxAttempts = 3,
                    AnswerTag = "/"
                };
                WriteConfigFile(configFilePath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                return defaultConfig;
            }

            string json = File.ReadAllText(configFilePath);
            return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }

        public static string GetConfigFilePath()
        {
            string path = GetCfgDirectory();
            Directory.CreateDirectory(path);
            return Path.Combine(path, "quiz.json");
        }

        public static Dictionary<string, Question> LoadQuestions()
        {
            string questionFilePath = GetQuestionConfigFilePath();
            if (!File.Exists(questionFilePath))
            {
                var questions = new Dictionary<string, Question>
                {
                    ["1"] = new Question { QuestionText = "В каком году произошел взрыв ЧАЭС?", Answer = "1986;1986г" },
                    ["2"] = new Question { QuestionText = "Сколько планет в солнечной системе?", Answer = "8" }
                };
                WriteConfigFile(questionFilePath, JsonConvert.SerializeObject(questions, Formatting.Indented));
                return questions;
            }

            string json = File.ReadAllText(questionFilePath);
            return JsonConvert.DeserializeObject<Dictionary<string, Question>>(json) ?? [];
        }

        public static string GetQuestionConfigFilePath()
        {
            string path = GetCfgDirectory();
            Directory.CreateDirectory(path);
            return Path.Combine(path, "quiz_questions.json");
        }

        public static void WriteConfigFile(string filePath, string content)
        {
            try
            {
                using StreamWriter writer = new(filePath);
                writer.Write(content);
            }
            catch (IOException ex)
            {
                throw new Exception($"Error writing config file: {ex.Message}");
            }
        }

        private static string GetCfgDirectory()
        {
            return Server.GameDirectory + "/csgo/addons/counterstrikesharp/configs/plugins/Quiz";
        }
    }
    public class Question
    {
        public string? QuestionText { get; set; }
        public string? Answer { get; set; }
    }
}
