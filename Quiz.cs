using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using Newtonsoft.Json;
using QuizApi;

namespace Quiz
{
    public class Quiz : BasePlugin
    {
        public override string ModuleName => "Quiz";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0";

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

        private static void QuestionCfg()
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
                        Console.WriteLine($"[Quiz] Loaded {questionsConfig.Count} questions.");
                    }
                    else
                    {
                        Console.WriteLine("[Quiz] No questions found in the configuration file.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Quiz] Error reading questions configuration: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[Quiz] Questions configuration file not found ({configFilePath}).");
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

        private static Question? GetRandomQuestion()
        {
            int maxQuestions = GetMaxQuestion();
            if (maxQuestions == 0)
            {
                Console.WriteLine("[Quiz] No questions available.");
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
            Random random = new();
            int a = random.Next(1, 101);
            int b = random.Next(1, 101);
            int result;
            string operation;
            int operationType = random.Next(1, 5);

            switch (operationType)
            {
                case 1:
                    result = a + b;
                    operation = "+";
                    break;
                case 2:
                    result = a - b;
                    operation = "-";
                    break;
                case 3:
                    result = a * b;
                    operation = "*";
                    break;
                case 4:
                    b = random.Next(1, 101);
                    a = b * random.Next(1, 10);
                    result = a / b;
                    operation = "/";
                    break;
                default:
                    throw new InvalidOperationException("Unknown operation");
            }

            CurrentAnswer = result.ToString();
            if (ChatPrefix != null && _cfg.AnswerTag != null)
            {
                BroadcastMessage($"{Localizer["Math", ChatPrefix, a, b, operation]}");
                BroadcastMessage($"{Localizer["Example", ChatPrefix, _cfg.AnswerTag]}");
            }
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
                    Console.WriteLine($"{_api.GetTranslatedText("SystemName")} | module not found. Core is waiting for a module");
                }
                else
                {
                    Console.WriteLine($"{_api.GetTranslatedText("SystemName")} | Quiz module successfully detected, core fully active.");
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

            public int GetQuizValue() => quizValue;

            public string GetAnswer() => currentAnswer;

            public void SetAnswer(string answer) => currentAnswer = answer;

            public void TriggerPlayerWin(CCSPlayerController player)
            {
                OnPlayerWin?.Invoke(player);
            }

            public void HandleClientAnswer(CCSPlayerController player, string answer)
            {
                OnClientAnswered?.Invoke(player);
                if (answer.Equals(currentAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    TriggerPlayerWin(player);
                }
                else
                {
                    OnPlayerLose?.Invoke(player);
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
