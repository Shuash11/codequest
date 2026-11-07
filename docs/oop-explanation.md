// OOP explanation — CodeQuest (final exam project, increment I6)
// Every class, file, and line named below was verified against the actual
// source tree at delivery (2026-11-08).

# OOP Concepts in CodeQuest

The exam brief requires the four OOP concepts **implemented in the actual
source code**, not merely mentioned in documentation
(`docs/oop-final-exam-guidelines.md`, lines 170-171). Each section below names
the real classes and locations.

## 1. Encapsulation

Private state behind public behavior — no public mutable field anywhere in the
models:

- `Quiz` (`src/CodeQuest/Models/Quiz.cs:18`) holds its questions in the private
  list `_questions` (line 26) and publishes only the read-only view
  `Questions`; the only way in is `AddQuestion(question)`, which null-checks.
  `FindQuestion(id)` looks a question up without exposing the list.
- `Question` (`src/CodeQuest/Models/Question.cs:22`) does the same with its
  choices: private `_choices` (line 30), public read-only `Choices`,
  `AddChoice` as the single entry point.
- `UserAccount` (`src/CodeQuest/Models/UserAccount.cs:17`) and `Choice`
  (`src/CodeQuest/Models/Choice.cs:17`) keep every scalar in a private field
  with a property over it.
- `UITheme` (`src/CodeQuest/UI/UITheme.cs:31`) encapsulates the whole visual
  system: forms never hard-code a color or size, so the D3 theme re-issue was
  a one-file change.

## 2. Inheritance

Base classes and derived classes with real shared behavior:

- `Question` (`src/CodeQuest/Models/Question.cs:22`) is an **abstract** base
  class. Its derived classes are `MultipleChoiceQuestion`
  (`src/CodeQuest/Models/MultipleChoiceQuestion.cs:16`) and
  `TrueFalseQuestion` (`src/CodeQuest/Models/TrueFalseQuestion.cs:16`). The
  base carries the shared concrete helpers (`GetCorrectChoice`,
  `IsCorrectChoice`, the private choice list), the derived classes carry what
  differs.
- `DataAccessException : Exception`
  (`src/CodeQuest/Data/DataExceptions.cs:21`) with seven derived, typed
  failures — `DuplicateEntityException` (line 124),
  `ReferentialIntegrityException` (line 140), `ConstraintViolationException`
  (line 155), `EntityNotFoundException` (line 170), `DataFormatException`
  (line 184), `AnswerValidationException` (line 199) — so callers catch one
  type and still know what happened.
- `ModulePanel : UserControl` (`src/CodeQuest/UI/IModulePanel.cs:41`) is the
  base of every real module screen (`TopicManageForm`, `QuizManageForm`,
  `QuizTakeForm`, `HistoryForm`, `ReportsForm`, `GenerateQuizForm`,
  `QuizReviewForm` all derive from it) — the shell treats them uniformly.
- Every window derives from the framework (`LoginForm`/`MainForm`/`DialogForm`
  are `Form` subclasses; the module panels are `UserControl` subclasses).

## 3. Polymorphism

One contract, many behaviors, chosen at runtime:

- **Method overriding:** `QuestionKind`, `ExpectedChoiceCount`, and
  `ValidateAnswer` are abstract on `Question` and overridden by both derived
  kinds — `MultipleChoiceQuestion.QuestionKind => "Multiple Choice"` with 4
  options (lines 19/22/28) versus `TrueFalseQuestion.QuestionKind =>
  "True/False"` with 2 (lines 22/25/32). The scoring service scores either
  kind through the base type without branching on the kind.
- **Interface polymorphism:** `IQuizProvider`
  (`src/CodeQuest/Services/IQuizProvider.cs:27`) is implemented by
  `AiQuizProvider` (`src/CodeQuest/Services/AiQuizProvider.cs:33`, the remote
  generation tiers) and `OfflineQuizProvider`
  (`src/CodeQuest/Services/OfflineQuizProvider.cs:26`, the seeded-database
  tier that serves existing quizzes and refuses to author new ones). The
  generation chain walks providers through the interface.
- The same pattern repeats for the services the screens consume:
  `ScoringService : IScoringService`
  (`src/CodeQuest/Services/ScoringService.cs:20`) and
  `ReportService : IReportService`
  (`src/CodeQuest/Services/ReportService.cs:25`) — the forms depend on the
  interfaces only, so the math can change without touching a form.

## 4. Abstraction

Contracts that state *what*, never *how*:

- The seven data-layer interfaces in `src/CodeQuest/Data/` —
  `IUserRepository`, `ITopicRepository`, `IQuizRepository`,
  `IQuestionRepository`, `IChoiceRepository`, `IAttemptRepository`,
  `ISubmittedAnswerRepository` — describe exactly what the application needs
  from each table; the concrete ADO.NET implementations live under
  `src/CodeQuest/Data/Repositories/`. The UI never touches SQLite directly.
- The service contracts: `IScoringService`
  (`src/CodeQuest/Services/IScoringService.cs:81`),
  `IReportService` (`src/CodeQuest/Services/IReportService.cs:141`),
  `IQuizProvider` (line 27 above).
- The UI contracts: `IModulePanel` (`src/CodeQuest/UI/IModulePanel.cs:18`) and
  `ILeaveGuard` (line 28) — the shell hosts and guards any panel that
  implements them without knowing its internals.
- `Question` itself is an abstract class: an always-concrete question kind,
  with the kind-specific rules declared abstract and implemented by each
  derived class.

## Where the concepts meet the grading rubric

| Concept | Rubric line | Primary evidence |
|---|---|---|
| Encapsulation | classes/objects 4, encapsulation 4 | `Quiz`, `Question`, `UserAccount`, `Choice`, `UITheme` |
| Inheritance | base + derived 4 | `Question` → two kinds; `DataAccessException` tree; `ModulePanel` → seven screens |
| Polymorphism | overriding/interfaces 4 | `ValidateAnswer` overrides; `IQuizProvider` chain; service interfaces |
| Abstraction | abstract classes/interfaces 4 | the repository contracts; `Question`; `IModulePanel`/`ILeaveGuard` |
