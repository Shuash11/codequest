// The quiz-taking screen.

using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodeQuest.UI;

public sealed class QuizTakeForm : ModulePanel, ILeaveGuard
{
    private static readonly Color[] TileColors =
        { UITheme.TileRed, UITheme.TileBlue, UITheme.TileAmber, UITheme.TileGreen };

    private const string EmptyTitle = "No quizzes yet.";
    private const string ResultSavedMessage = "Result saved.";
    private const string LeaveTitle = "Leave this quiz?";
    private const string LeaveMessage = "Your answers so far will be discarded.";
    private const string LeaveConfirmText = "Leave";
    private const string FeedbackCorrect = "Correct!";
    private const string FeedbackWrong = "Incorrect \u2014 the green tile was the answer.";

    private readonly QuizRepository _quizzes;
    private readonly QuestionRepository _questions;
    private readonly TopicRepository _topics;
    private readonly AttemptRepository _attempts;
    private readonly SubmittedAnswerRepository _answers;
    private readonly IScoringService _scoring;
    private readonly UserAccount _user;
    private readonly IGamificationRepository _gamification;
    private readonly IAchievementEvaluator _achievements;

    private FlowLayoutPanel _cardFlow = null!;
    private Panel _emptyView = null!;
    private Panel _runView = null!;
    private Label _runTitle = null!;
    private Label _runProgressLabel = null!;
    private ProgressTrack _runProgress = null!;
    private Label _questionText = null!;
    private Label _feedback = null!;
    private Panel _choiceStack = null!;
    private FlatButton _previousButton = null!;
    private FlatButton _nextButton = null!;
    private Panel _resultView = null!;
    private Label _resultScore = null!;
    private Label _resultPercent = null!;
    private Label _resultMessage = null!;
    private Label _resultContext = null!;
    private Panel _resultListCard = null!;
    private Panel _resultActions = null!;
    private Panel _heroCard = null!;
    private FlatButton _viewAnswersButton = null!;
    private FlatButton _retakeButton = null!;
    private FlatButton _doneButton = null!;
    private AttemptReviewForm? _reviewPanel;

    private Panel _resultReveal = null!;
    private Label _resultXpLine = null!;
    private LevelChip _resultLevelChip = null!;
    private ProgressTrack _resultLevelBar = null!;
    private Label _resultLevelCaption = null!;
    private Label _resultAchievementsCaption = null!;
    private FlowLayoutPanel _resultAchievementsFlow = null!;

    private Dictionary<int, string> _topicNames = new();

    private Quiz? _runQuiz;
    private int?[] _selections = Array.Empty<int?>();
    private bool[] _revealed = Array.Empty<bool>();
    private int _runIndex;
    private bool _submitting;
    private List<AnswerTile> _tiles = new();

    private int _resultAttemptId;
    private Quiz? _resultQuiz;
    private int _resultCorrect;
    private bool _awaitingSavedAnnouncement;
    private long _resultXpEarned;
    private UserProgress? _resultProgress;
    private IReadOnlyList<AchievementDefinition> _resultNewlyUnlocked = Array.Empty<AchievementDefinition>();

    public QuizTakeForm(
        QuizRepository quizzes,
        QuestionRepository questions,
        TopicRepository topics,
        AttemptRepository attempts,
        SubmittedAnswerRepository answers,
        IScoringService scoring,
        UserAccount user,
        IGamificationRepository gamification,
        IAchievementEvaluator achievements)
    {
        ArgumentNullException.ThrowIfNull(quizzes);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(gamification);
        ArgumentNullException.ThrowIfNull(achievements);
        _quizzes = quizzes;
        _questions = questions;
        _topics = topics;
        _attempts = attempts;
        _answers = answers;
        _scoring = scoring;
        _user = user;
        _gamification = gamification;
        _achievements = achievements;

        Dock = DockStyle.Fill;
        BackColor = UITheme.Background;

        _cardFlow = new FlowLayoutPanel
        {
            Name = "TakeQuizCardFlow",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UITheme.Background,
        };

        _emptyView = new Panel
        {
            Name = "TakeQuizEmptyView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };
        var emptyCard = new CardPanel
        {
            Name = "TakeQuizEmptyCard",
            Size = new Size(UITheme.Scaled(this, 480), UITheme.Scaled(this, 130)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        var emptyTitle = new Label
        {
            Name = "TakeQuizEmptyTitle",
            Text = EmptyTitle,
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(UITheme.Scaled(this, 448), UITheme.Scaled(this, 22)),
        };
        var emptyCaption = new Label
        {
            Name = "TakeQuizEmptyCaption",
            Text = "Generate a quiz and approve it \u2014 it will show up here.",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16) + UITheme.Scaled(this, 26)),
            Size = new Size(UITheme.Scaled(this, 448), UITheme.Scaled(this, 18)),
        };
        emptyCard.Controls.Add(emptyTitle);
        emptyCard.Controls.Add(emptyCaption);
        _emptyView.Controls.Add(emptyCard);

        _runView = BuildRunView();
        _resultView = BuildResultView();

        Controls.Add(_cardFlow);
        Controls.Add(_runView);
        Controls.Add(_resultView);
        Controls.Add(_emptyView);

        _runView.Resize += static (sender, e) =>
        {
            if (sender is Panel run && run.Visible && run.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.LayoutRunHeader();
                form.LayoutQuestionCard();
            }
        };

        RefreshData();
    }

    public override void RefreshData()
    {
        Dictionary<int, string> names = new();
        foreach (Topic topic in _topics.GetAll())
        {
            names[topic.TopicId] = topic.Name;
        }
        _topicNames = names;

        LoadQuizCards();
    }

    public bool ConfirmLeave()
    {
        if (_runQuiz is null || _submitting)
        {
            return true;
        }

        if (!DialogForm.ShowConfirm(LeaveTitle, LeaveMessage, LeaveConfirmText, secondaryText: "Stay"))
        {
            return false;
        }

        DiscardRun();
        return true;
    }

    private void LoadQuizCards()
    {
        IReadOnlyList<Quiz> quizzes = _quizzes.GetAll();

        _cardFlow.SuspendLayout();
        _cardFlow.Controls.Clear();

        foreach (Quiz quiz in quizzes)
        {
            string topicName = _topicNames.TryGetValue(quiz.TopicId, out string? name) ? name : string.Empty;
            int questionCount = _questions.GetByQuiz(quiz.QuizId).Count;

            var card = new QuizCard(quiz, topicName, questionCount)
            {
                Margin = new Padding(0, 0, UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing16)),
            };
            card.PlayClicked += static (sender, quizId) =>
            {
                if (sender is QuizCard card2 && card2.FindAncestor<QuizTakeForm>() is { } form)
                {
                    form.StartQuiz(quizId);
                }
            };
            _cardFlow.Controls.Add(card);
        }

        _cardFlow.ResumeLayout();

        bool empty = quizzes.Count == 0;
        _emptyView.Visible = empty;
        _cardFlow.Visible = !empty;
    }

    private Panel BuildRunView()
    {
        var run = new Panel
        {
            Name = "TakeRunView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
        };

        var footer = new Panel
        {
            Name = "RunFooter",
            Dock = DockStyle.Bottom,
            Height = UITheme.Scaled(this, 40),
            BackColor = UITheme.Background,
        };
        _previousButton = new FlatButton
        {
            Name = "PreviousButton",
            Text = "Previous",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, UITheme.Scaled(this, 4)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
        };
        _previousButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.MoveQuestion(-1);
            }
        };
        _nextButton = new FlatButton
        {
            Name = "NextButton",
            Text = "Next",
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing12), UITheme.Scaled(this, 4)),
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 32)),
        };
        _nextButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.HandleNext();
            }
        };
        footer.Controls.Add(_previousButton);
        footer.Controls.Add(_nextButton);

        _runTitle = new Label
        {
            Name = "RunQuizTitle",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Background,
            AutoSize = false,
        };
        _runProgressLabel = new Label
        {
            Name = "RunQuestionCount",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Background,
            AutoSize = false,
        };
        _runProgress = new ProgressTrack { Name = "RunProgressTrack", Dock = DockStyle.Top };

        var header = new Panel
        {
            Name = "RunHeader",
            Dock = DockStyle.Top,
            Height = UITheme.Scaled(this, 52),
            BackColor = UITheme.Background,
        };
        header.Controls.Add(_runTitle);
        header.Controls.Add(_runProgressLabel);

        var card = new CardPanel { Name = "RunQuestionCard", Dock = DockStyle.Fill };
        _questionText = new Label
        {
            Name = "RunQuestionText",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = false,
        };
        _feedback = new Label
        {
            Name = "RunFeedback",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.SuccessText,
            BackColor = UITheme.Surface,
            AutoSize = false,
            Visible = false,
        };
        _choiceStack = new Panel { Name = "RunChoiceStack", BackColor = UITheme.Surface };
        card.Controls.Add(_questionText);
        card.Controls.Add(_feedback);
        card.Controls.Add(_choiceStack);

        run.Controls.Add(card);
        run.Controls.Add(_runProgress);
        run.Controls.Add(header);
        run.Controls.Add(footer);
        return run;
    }

    private void LayoutRunHeader()
    {
        if (_runTitle.Parent is not Panel header)
        {
            return;
        }

        int titleHeight = UITheme.Scaled(this, 22);
        _runTitle.Location = new Point(0, 0);
        _runTitle.Size = new Size(header.Width, titleHeight);
        _runProgressLabel.Location = new Point(0, titleHeight + UITheme.Scaled(this, UITheme.Spacing4));
        _runProgressLabel.Size = new Size(header.Width, UITheme.Scaled(this, 18));
    }

    private void LayoutQuestionCard()
    {
        if (_questionText.Parent is not CardPanel card)
        {
            return;
        }

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int contentWidth = Math.Max(card.Width - inset * 2, 1);
        int textHeight = TextRenderer.MeasureText(
            _questionText.Text.Length == 0 ? " " : _questionText.Text,
            _questionText.Font,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak)
            .Height;

        _questionText.Location = new Point(inset, inset);
        _questionText.Size = new Size(contentWidth, textHeight);

        int top = inset + textHeight + UITheme.Scaled(this, UITheme.Spacing12);

        if (_feedback.Visible)
        {
            _feedback.Location = new Point(inset, top);
            _feedback.Size = new Size(contentWidth, UITheme.Scaled(this, 22));
            top += UITheme.Scaled(this, 22) + UITheme.Scaled(this, UITheme.Spacing8);
        }

        _choiceStack.Location = new Point(inset, top);
        _choiceStack.Size = new Size(contentWidth, Math.Max(card.Height - top - inset, 1));

        int top2 = 0;
        int tileHeight = UITheme.Scaled(this, 54);
        int tileGap = UITheme.Scaled(this, UITheme.Spacing8);
        foreach (AnswerTile tile in _tiles)
        {
            tile.Location = new Point(0, top2);
            tile.Size = new Size(_choiceStack.Width, tileHeight);
            top2 += tileHeight + tileGap;
        }
    }

    internal void StartNewQuiz(int quizId) => StartQuiz(quizId);

    private void StartQuiz(int quizId)
    {
        Quiz? quiz = _quizzes.GetById(quizId);
        if (quiz is null || quiz.Status != QuizStatus.Approved)
        {
            return;
        }

        if (quiz.Questions.Count == 0)
        {
            ReportStatus("This quiz has no questions yet.", success: false);
            return;
        }

        _runQuiz = quiz;
        _selections = new int?[quiz.Questions.Count];
        _revealed = new bool[quiz.Questions.Count];
        _runIndex = 0;

        _runTitle.Text = quiz.Title;
        ShowView(_runView);
        RenderQuestion();
    }

    private void DiscardRun()
    {
        _runQuiz = null;
        _selections = Array.Empty<int?>();
        _revealed = Array.Empty<bool>();
        _runIndex = 0;
        _tiles = new List<AnswerTile>();
        ShowList(announceCompletion: false);
    }

    private void RenderQuestion()
    {
        if (_runQuiz is null)
        {
            return;
        }

        Question question = _runQuiz.Questions[_runIndex];

        _runProgressLabel.Text = $"Question {_runIndex + 1} of {_runQuiz.Questions.Count}";
        UpdateRunProgress();
        _questionText.Text = question.QuestionText;
        _feedback.Visible = false;

        _choiceStack.SuspendLayout();
        foreach (AnswerTile existing in _tiles)
        {
            existing.Dispose();
        }
        _choiceStack.Controls.Clear();
        _tiles = new List<AnswerTile>();

        Choice? key = question.GetCorrectChoice();
        Choice? picked = _selections[_runIndex] is { } pickedId
            ? question.Choices.FirstOrDefault(choice => choice.ChoiceId == pickedId)
            : null;

        var ordered = question.Choices.OrderBy(item => item.Position).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            Choice choice = ordered[i];
            bool revealed = _revealed[_runIndex];
            var tile = new AnswerTile(i, choice.ChoiceText, TileColors[i % TileColors.Length])
            {
                Name = $"AnswerTile{choice.ChoiceId}",
                ChoiceId = choice.ChoiceId,
                IsSelected = picked is not null && picked.ChoiceId == choice.ChoiceId,
                IsCorrectTile = choice.IsCorrect,
                IsRevealed = revealed,
            };
            tile.Click += static (sender, e) =>
            {
                if (sender is AnswerTile tile2 && tile2.FindAncestor<QuizTakeForm>() is { } form)
                {
                    form.HandleTileClick(tile2);
                }
            };
            _choiceStack.Controls.Add(tile);
            _tiles.Add(tile);
        }
        _choiceStack.ResumeLayout();

        if (_revealed[_runIndex] && picked is not null)
        {
            ShowFeedback(question.IsCorrectChoice(picked));
        }

        LayoutQuestionCard();

        _previousButton.Enabled = _runIndex > 0;
        bool last = _runIndex == _runQuiz.Questions.Count - 1;
        _nextButton.Text = last ? "Submit" : "Next";
        LayoutRunHeader();
    }

    private void ShowFeedback(bool correct)
    {
        _feedback.Text = correct ? FeedbackCorrect : FeedbackWrong;
        _feedback.ForeColor = correct ? UITheme.SuccessText : UITheme.DangerText;
        _feedback.Visible = true;
    }

    private void UpdateRunProgress()
    {
        if (_runQuiz is null)
        {
            return;
        }

        _runProgress.Value = _revealed.Count(revealed => revealed) / (double)_runQuiz.Questions.Count;
    }

    private void HandleTileClick(AnswerTile tile)
    {
        if (_runQuiz is null || _submitting || _revealed[_runIndex])
        {
            return;
        }

        int choiceId = tile.ChoiceId;

        _selections[_runIndex] = choiceId;
        _revealed[_runIndex] = true;

        Question question = _runQuiz.Questions[_runIndex];
        Choice picked = question.Choices.First(choice => choice.ChoiceId == choiceId);
        bool correct = question.IsCorrectChoice(picked);

        foreach (AnswerTile other in _tiles)
        {
            other.IsSelected = other == tile;
            other.IsRevealed = true;
            other.IsCorrectTile = other.ChoiceId == question.GetCorrectChoice()?.ChoiceId;
        }

        ShowFeedback(correct);
        UpdateRunProgress();
        LayoutQuestionCard();
    }

    private void MoveQuestion(int delta)
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        int target = _runIndex + delta;
        if (target < 0 || target >= _runQuiz.Questions.Count)
        {
            return;
        }

        _runIndex = target;
        RenderQuestion();
    }

    private void HandleNext()
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        if (_runIndex < _runQuiz.Questions.Count - 1)
        {
            MoveQuestion(1);
            return;
        }

        Submit();
    }

    private void Submit()
    {
        if (_runQuiz is null || _submitting)
        {
            return;
        }

        _submitting = true;
        _previousButton.Enabled = false;
        _nextButton.Enabled = false;

        Quiz quiz = _runQuiz;
        var answers = new List<SubmittedAnswer>();
        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            if (_selections[i] is not { } choiceId)
            {
                continue;
            }

            answers.Add(new SubmittedAnswer
            {
                QuestionId = quiz.Questions[i].QuestionId,
                ChosenChoiceId = choiceId,
            });
        }

        try
        {
            QuizScoreResult score = _scoring.Score(quiz, answers);
            var attempt = new QuizAttempt
            {
                QuizId = quiz.QuizId,
                UserId = _user.UserId,
                Score = score.CorrectCount,
                AttemptedAt = DateTimeOffset.UtcNow,
            };
            _attempts.AddWithAnswers(attempt, answers);

            _resultXpEarned = (long)score.CorrectCount * DifficultyXpWeights.For(quiz.Difficulty);
            (_resultProgress, _resultNewlyUnlocked) = RecordProgress();

            _resultAttemptId = attempt.AttemptId;
            _resultQuiz = quiz;
            _resultCorrect = score.CorrectCount;
            _awaitingSavedAnnouncement = true;
            _runQuiz = null;
            ShowResult();
        }
        catch (Exception)
        {
            EndBusyAfterFailure();
            ReportStatus("The quiz could not be saved. Please try again.", success: false);
            return;
        }
        finally
        {
            _submitting = false;
            _previousButton.Enabled = _runIndex > 0;
            _nextButton.Enabled = true;
        }
    }

    private void EndBusyAfterFailure()
    {
        _previousButton.Enabled = _runIndex > 0;
        _nextButton.Enabled = true;
    }

    private (UserProgress? Progress, IReadOnlyList<AchievementDefinition> NewlyUnlocked) RecordProgress()
    {
        try
        {
            UserProgress progress = _gamification.GetUserProgress(_user.UserId);
            UserGamificationStats stats = _gamification.GetUserStats(_user.UserId);
            IReadOnlyList<AchievementDefinition> earned = _achievements.Evaluate(
                new AchievementContext(progress.Level, stats));

            if (earned.Count == 0)
            {
                return (progress, Array.Empty<AchievementDefinition>());
            }

            IReadOnlyList<UnlockedAchievement> recorded = _gamification.AddAchievementsIfNew(
                _user.UserId,
                earned.Select(definition => definition.Key));

            if (recorded.Count == 0)
            {
                return (progress, Array.Empty<AchievementDefinition>());
            }

            HashSet<string> recordedKeys = recorded.Select(unlocked => unlocked.Key).ToHashSet();
            return (progress, earned.Where(definition => recordedKeys.Contains(definition.Key)).ToArray());
        }
        catch (Exception)
        {
            ReportStatus("The score was saved, but progress could not be recorded.", success: false);
            return (null, Array.Empty<AchievementDefinition>());
        }
    }

    private Panel BuildResultView()
    {
        var result = new Panel
        {
            Name = "TakeResultView",
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = UITheme.Background,
            AutoScroll = true,
        };

        int cardWidth = UITheme.Scaled(this, 560);
        var hero = new CardPanel
        {
            Name = "ResultHeroCard",
            Location = new Point(0, 0),
            Size = new Size(cardWidth, UITheme.Scaled(this, 150)),
        };
        _heroCard = hero;
        _resultScore = new Label
        {
            Name = "ResultScoreLabel",
            Font = UITheme.Display,
            ForeColor = UITheme.AccentText,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, UITheme.Spacing12)),
        };
        _resultPercent = new Label
        {
            Name = "ResultPercentLabel",
            Font = UITheme.Body,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
        };
        _resultMessage = new Label
        {
            Name = "ResultMessageLabel",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.TextPrimary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing16),
                UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 52)),
        };
        _resultContext = new Label
        {
            Name = "ResultContextLabel",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(
                UITheme.Scaled(this, UITheme.Spacing16),
                UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 76)),
        };
        hero.Controls.Add(_resultScore);
        hero.Controls.Add(_resultPercent);
        hero.Controls.Add(_resultMessage);
        hero.Controls.Add(_resultContext);

        _resultReveal = new Panel
        {
            Name = "ResultReveal",
            BackColor = UITheme.Surface,
            Location = new Point(UITheme.Scaled(this, UITheme.Spacing16), UITheme.Scaled(this, 104)),
            Size = new Size(Math.Max(cardWidth - UITheme.Scaled(this, UITheme.Spacing32), 1), 0),
        };

        _resultXpLine = new Label
        {
            Name = "ResultXpLine",
            Font = UITheme.BodySemibold,
            ForeColor = UITheme.AccentText,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Location = new Point(0, 0),
        };

        _resultLevelChip = new LevelChip
        {
            Name = "ResultLevelChip",
            Location = new Point(0, UITheme.Scaled(this, UITheme.Spacing8 + 18)),
        };

        _resultLevelBar = new ProgressTrack
        {
            Name = "ResultLevelBar",
            AccessibleName = "Level progress",
        };

        _resultLevelCaption = new Label
        {
            Name = "ResultLevelCaption",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Size = new Size(UITheme.Scaled(this, 120), UITheme.Scaled(this, 16)),
        };

        _resultAchievementsCaption = new Label
        {
            Name = "ResultAchievementsCaption",
            Text = "Achievements unlocked",
            Font = UITheme.Caption,
            ForeColor = UITheme.TextSecondary,
            BackColor = UITheme.Surface,
            AutoSize = true,
            Visible = false,
        };

        _resultAchievementsFlow = new FlowLayoutPanel
        {
            Name = "ResultAchievementsFlow",
            BackColor = UITheme.Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Visible = false,
            TabStop = false,
        };

        _resultReveal.Controls.Add(_resultXpLine);
        _resultReveal.Controls.Add(_resultLevelChip);
        _resultReveal.Controls.Add(_resultLevelBar);
        _resultReveal.Controls.Add(_resultLevelCaption);
        _resultReveal.Controls.Add(_resultAchievementsCaption);
        _resultReveal.Controls.Add(_resultAchievementsFlow);
        hero.Controls.Add(_resultReveal);

        result.Controls.Add(hero);

        _resultListCard = new CardPanel
        {
            Name = "ResultListCard",
            Location = new Point(0, hero.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 100)),
        };
        result.Controls.Add(_resultListCard);

        _resultActions = new Panel
        {
            Name = "ResultActions",
            Location = new Point(0, _resultListCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16)),
            Size = new Size(cardWidth, UITheme.Scaled(this, 32)),
        };
        int buttonHeight = UITheme.Scaled(this, 32);
        _viewAnswersButton = new FlatButton
        {
            Name = "ViewAnswersButton",
            Text = "View answers",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(0, 0),
            Size = new Size(UITheme.Scaled(this, 140), buttonHeight),
        };
        _viewAnswersButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.OpenReview();
            }
        };
        _retakeButton = new FlatButton
        {
            Name = "RetakeButton",
            Text = "Retake",
            Location = new Point(UITheme.Scaled(this, 140) + UITheme.Scaled(this, UITheme.Spacing12), 0),
            Size = new Size(UITheme.Scaled(this, 110), buttonHeight),
        };
        _retakeButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.Retake();
            }
        };
        _doneButton = new FlatButton
        {
            Name = "DoneButton",
            Text = "Done",
            Variant = UITheme.FlatButtonVariant.Secondary,
            Location = new Point(UITheme.Scaled(this, 250) + UITheme.Scaled(this, UITheme.Spacing24), 0),
            Size = new Size(UITheme.Scaled(this, 110), buttonHeight),
        };
        _doneButton.Click += static (sender, e) =>
        {
            if (sender is FlatButton button && button.FindAncestor<QuizTakeForm>() is { } form)
            {
                form.ShowList(announceCompletion: true);
            }
        };
        _resultActions.Controls.Add(_viewAnswersButton);
        _resultActions.Controls.Add(_retakeButton);
        _resultActions.Controls.Add(_doneButton);
        result.Controls.Add(_resultActions);

        return result;
    }

    private void ShowResult()
    {
        if (_resultQuiz is null)
        {
            return;
        }

        int total = _resultQuiz.Questions.Count;
        double percent = total == 0
            ? 0d
            : Math.Round(_resultCorrect * 100d / total, MidpointRounding.AwayFromZero);
        string topicName = _topicNames.TryGetValue(_resultQuiz.TopicId, out string? name) ? name : string.Empty;

        _resultScore.Text = $"{_resultCorrect} / {total}";
        _resultPercent.Text = $"{percent:0}%";
        _resultPercent.Location = new Point(
            _resultScore.Right + UITheme.Scaled(this, UITheme.Spacing12),
            UITheme.Scaled(this, UITheme.Spacing12) + UITheme.Scaled(this, 18));
        _resultMessage.Text = percent switch
        {
            >= 80d => "Excellent work!",
            >= 50d => "Nice run \u2014 keep going!",
            _ => "Keep practicing \u2014 you'll get there!",
        };
        _resultMessage.ForeColor = percent switch
        {
            >= 80d => UITheme.SuccessText,
            >= 50d => UITheme.LevelMedium,
            _ => UITheme.DangerText,
        };
        _resultContext.Text = $"{topicName} \u2014 {_resultQuiz.Difficulty}";

        FillReveal();
        LayoutReveal();
        SizeHeroForResult();
        _resultListCard.Location = new Point(0, _heroCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16));
        _resultListCard.Width = Math.Max(_heroCard.Width, 1);

        BuildResultRows(_resultQuiz);
        _resultActions.Location = new Point(0, _resultListCard.Bottom + UITheme.Scaled(this, UITheme.Spacing16));
        ShowView(_resultView);

        this.FindAncestor<MainForm>()?.UpdateSidebarLevel();
    }

    private void SizeHeroForResult()
    {
        if (_resultReveal.Visible)
        {
            _heroCard.Height = _resultReveal.Bottom + UITheme.Scaled(this, UITheme.Spacing16);
        }
        else
        {
            _heroCard.Height = UITheme.Scaled(this, 150);
        }
    }

    private void FillReveal()
    {
        if (_resultProgress is not { } progress)
        {
            _resultReveal.Visible = false;
            return;
        }

        _resultReveal.Visible = true;
        _resultXpLine.Text = "+" + _resultXpEarned.ToString(System.Globalization.CultureInfo.InvariantCulture) + " XP";
        _resultLevelChip.SetLevel(progress.Level);
        _resultLevelBar.Value = progress.ProgressFraction;

        _resultLevelCaption.Text = progress.XpForNextLevel == 0
            ? "Max level"
            : (progress.XpForNextLevel - progress.XpIntoLevel).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " XP to Lvl " + (progress.Level + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (Control existing in _resultAchievementsFlow.Controls)
        {
            existing.Dispose();
        }
        _resultAchievementsFlow.Controls.Clear();

        bool any = _resultNewlyUnlocked.Count > 0;
        _resultAchievementsCaption.Visible = any;
        _resultAchievementsFlow.Visible = any;

        foreach (AchievementDefinition definition in _resultNewlyUnlocked)
        {
            _resultAchievementsFlow.Controls.Add(new AchievementChipPanel(definition));
        }
    }

    private void LayoutReveal()
    {
        if (!_resultReveal.Visible)
        {
            return;
        }

        int revealWidth = Math.Max(_resultReveal.Width, 1);
        int rowGap = UITheme.Scaled(this, UITheme.Spacing8);
        int blockGap = UITheme.Scaled(this, UITheme.Spacing16);

        _resultLevelChip.Location = new Point(0, _resultXpLine.Bottom + rowGap);

        Size captionSize = TextRenderer.MeasureText(_resultLevelCaption.Text, UITheme.Caption);
        _resultLevelCaption.Size = new Size(Math.Max(captionSize.Width, UITheme.Scaled(this, 40)), UITheme.Scaled(this, 16));
        _resultLevelCaption.Location = new Point(
            Math.Max(revealWidth - _resultLevelCaption.Width, 0),
            _resultLevelChip.Location.Y + ((_resultLevelChip.Height - _resultLevelCaption.Height) / 2));

        int barWidth = Math.Max(
            _resultLevelCaption.Left - _resultLevelChip.Right - rowGap,
            UITheme.Scaled(this, 40));
        _resultLevelBar.Location = new Point(
            _resultLevelChip.Right + rowGap,
            _resultLevelChip.Location.Y + ((_resultLevelChip.Height - UITheme.Scaled(this, 8)) / 2));
        _resultLevelBar.Size = new Size(barWidth, UITheme.Scaled(this, 8));

        int rowBottom = Math.Max(_resultLevelChip.Bottom, _resultLevelBar.Bottom);

        if (_resultAchievementsCaption.Visible)
        {
            _resultAchievementsFlow.Width = Math.Max(revealWidth, 1);
            _resultAchievementsCaption.Location = new Point(0, rowBottom + blockGap);
            _resultAchievementsFlow.Location = new Point(0, _resultAchievementsCaption.Bottom + rowGap);
            _resultReveal.Height = _resultAchievementsFlow.Bottom;
        }
        else
        {
            _resultReveal.Height = rowBottom;
        }
    }

    private sealed class AchievementChipPanel : Panel
    {
        private const int ChipHeight = 28;

        private readonly string _glyph;
        private readonly string _title;

        public AchievementChipPanel(AchievementDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            Name = "ResultAchievementChip" + definition.Key;
            TabStop = false;
            _glyph = definition.Glyph;
            _title = definition.Title;

            int inset = UITheme.Scaled(this, UITheme.Spacing8);
            int glyphWidth = TextRenderer.MeasureText(_glyph, UITheme.NavGlyph).Width;
            int titleWidth = TextRenderer.MeasureText(_title, UITheme.CaptionSemibold).Width;
            Width = inset + glyphWidth + UITheme.Scaled(this, UITheme.Spacing4) + titleWidth + inset;
            Height = UITheme.Scaled(this, ChipHeight);
            Margin = new Padding(0, 0, UITheme.Scaled(this, UITheme.Spacing8), 0);
            AccessibleName = _title;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(UITheme.ContainerSurface(this));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var bounds = new Rectangle(0, 0, Width, Height);
            using GraphicsPath path = UITheme.RoundedPath(bounds, UITheme.Scaled(this, UITheme.RadiusChip));
            using var brush = new SolidBrush(UITheme.AccentTint);
            e.Graphics.FillPath(brush, path);

            int inset = UITheme.Scaled(this, UITheme.Spacing8);
            int glyphWidth = TextRenderer.MeasureText(_glyph, UITheme.NavGlyph).Width;
            var glyphBounds = new Rectangle(inset, 0, glyphWidth, Height);
            TextRenderer.DrawText(
                e.Graphics,
                _glyph,
                UITheme.NavGlyph,
                glyphBounds,
                UITheme.AccentText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            var titleBounds = new Rectangle(
                inset + glyphWidth + UITheme.Scaled(this, UITheme.Spacing4),
                0,
                Math.Max(Width - inset - glyphWidth - UITheme.Scaled(this, UITheme.Spacing4) - inset, 1),
                Height);
            TextRenderer.DrawText(
                e.Graphics,
                _title,
                UITheme.CaptionSemibold,
                titleBounds,
                UITheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    private void BuildResultRows(Quiz quiz)
    {
        _resultListCard.SuspendLayout();
        foreach (Control existing in _resultListCard.Controls)
        {
            existing.Dispose();
        }
        _resultListCard.Controls.Clear();

        int inset = UITheme.Scaled(this, UITheme.Spacing16);
        int rowHeight = UITheme.Scaled(this, 44);
        int top = inset;

        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            Question question = quiz.Questions[i];
            int? selected = _selections.Length > i ? _selections[i] : null;
            bool correct = selected is { } chosenId
                && question.IsCorrectChoice(question.Choices.FirstOrDefault(choice => choice.ChoiceId == chosenId));

            var row = new Panel
            {
                Name = $"ResultRow{i}",
                Location = new Point(inset, top),
                Size = new Size(Math.Max(_resultListCard.Width - inset * 2, 1), rowHeight),
                BackColor = UITheme.Surface,
            };

            Color verdictColor = correct ? UITheme.SuccessText : UITheme.DangerText;
            var mark = new Label
            {
                Name = $"ResultMark{i}",
                Text = correct ? "\u2713" : "\u2717",
                Font = UITheme.NavGlyph,
                ForeColor = verdictColor,
                BackColor = UITheme.Surface,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0),
                Size = new Size(UITheme.Scaled(this, 24), rowHeight),
            };
            var verdict = new Label
            {
                Name = $"ResultVerdict{i}",
                Text = correct ? "Correct" : "Incorrect",
                Font = UITheme.Body,
                ForeColor = verdictColor,
                BackColor = UITheme.Surface,
                AutoSize = false,
                Location = new Point(UITheme.Scaled(this, 24), 0),
                Size = new Size(UITheme.Scaled(this, 90), rowHeight),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var detail = new Label
            {
                Name = $"ResultQuestion{i}",
                Text = $"Question {i + 1}   {question.QuestionText}",
                Font = UITheme.Body,
                ForeColor = UITheme.TextPrimary,
                BackColor = UITheme.Surface,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(UITheme.Scaled(this, 24) + UITheme.Scaled(this, 90), 0),
                Size = new Size(
                    Math.Max(row.Width - UITheme.Scaled(this, 24) - UITheme.Scaled(this, 90), 1),
                    rowHeight),
            };
            row.Controls.Add(mark);
            row.Controls.Add(verdict);
            row.Controls.Add(detail);
            _resultListCard.Controls.Add(row);
            top += rowHeight;

            if (i < quiz.Questions.Count - 1)
            {
                var separator = new Panel
                {
                    Name = $"ResultSeparator{i}",
                    Location = new Point(inset, top),
                    Size = new Size(Math.Max(_resultListCard.Width - inset * 2, 1), UITheme.Scaled(this, 1)),
                    BackColor = UITheme.SeparatorHairline,
                };
                _resultListCard.Controls.Add(separator);
                top += UITheme.Scaled(this, 1);
            }
        }

        _resultListCard.ResumeLayout();
        _resultListCard.Height = top + inset;
    }

    private void OpenReview()
    {
        if (_resultAttemptId == 0)
        {
            return;
        }

        if (_reviewPanel is null)
        {
            _reviewPanel = new AttemptReviewForm(_attempts, _quizzes, _answers)
            {
                Name = "TakeResultReviewPanel",
                Dock = DockStyle.Fill,
                Visible = false,
            };
            _reviewPanel.BackRequested += static (sender, e) =>
            {
                if (sender is AttemptReviewForm review && review.FindAncestor<QuizTakeForm>() is { } form)
                {
                    review.Visible = false;
                    form.ShowResultFromReview();
                }
            };
            Controls.Add(_reviewPanel);
        }

        ShowDrillIn(visible: false);
        _reviewPanel.Open(_resultAttemptId);
        _reviewPanel.Visible = true;
        _reviewPanel.BringToFront();
    }

    private void ShowResultFromReview()
    {
        ShowView(_resultView);
    }

    private void Retake()
    {
        if (_resultQuiz is null)
        {
            return;
        }

        StartQuiz(_resultQuiz.QuizId);
    }

    private void ShowList(bool announceCompletion)
    {
        bool announce = announceCompletion && _awaitingSavedAnnouncement;
        RefreshData();

        if (announce)
        {
            _awaitingSavedAnnouncement = false;
            ReportStatus(ResultSavedMessage, success: true);
        }

        ShowView(_cardFlow);
    }

    private void ShowView(Control view)
    {
        foreach (Control child in Controls)
        {
            if (child is not AttemptReviewForm)
            {
                child.Visible = child == view;
            }
        }
    }

    private void ShowDrillIn(bool visible)
    {
        _cardFlow.Visible = visible;
        _runView.Visible = visible;
        _resultView.Visible = visible;
    }
}
