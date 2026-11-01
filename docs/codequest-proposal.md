CodeQuest

AI-Powered Programming and Cybersecurity Quiz System

# 1. Concept / Project Description

CodeQuest is a desktop application that helps students practice and test their knowledge of programming and cybersecurity. Students can choose topics such as C#, Python, Java, C++, JavaScript, SQL, networking, web security, and other related subjects. They can also choose a difficulty level and the number of questions for each quiz.

The system uses an AI API to create a randomized set of quiz questions based on the student’s choices. The generated questions, choices, correct answers, and explanations are saved in a local database. After the student finishes a quiz, the system checks the answers, calculates the score, and saves the result. Previously generated quizzes and saved results can still be opened without an internet connection.

# 2. Technology to Be Used

# 3. API Approach

The system will use the REST API approach to communicate with Google Gemini. When a student selects a topic, difficulty level, and question count, the application sends a request through C# HttpClient. The AI returns the quiz in JSON format, including the questions, answer choices, correct answers, and short explanations. The application reads the JSON response and saves the quiz locally.

# 4. Database Approach

SQLite will be used as the local database, so there is no need to set up a separate database server. It will store user accounts, quiz topics, generated quizzes, questions, answer choices, quiz attempts, and submitted answers. Each completed attempt will be saved with its score and date. This also allows students to review previous results and use quizzes that were saved earlier.

# 5. Main Features

Note: An internet connection is needed to generate new AI quizzes. Saved quizzes, quiz-taking, scoring, and local result storage will work offline. The AI-generated answer key should be checked for accuracy before a quiz is made available to students.


[TABLE 1]

A desktop quiz application for practicing programming and cybersecurity topics.


[TABLE 2]

Programming language | C#

Framework | .NET Windows Forms (WinForms)

Database | SQLite (local database)

Database library | Microsoft.Data.Sqlite

AI provider | Google Gemini API

API communication | REST API using HttpClient

Data format | JSON


[TABLE 3]

• AI-generated quizzes: Create questions based on the selected topic. | • Multiple topics: Practice programming, SQL, networking, and security.

• Difficulty selection: Choose easy, medium, or hard. | • Automatic scoring: Check answers and calculate each result.

• Quiz history: Review previous attempts and submitted answers. | • Offline access: Retake quizzes saved on the computer.

• Performance reports: See results and topics that need more practice. | 
