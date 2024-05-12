# Quiz
**The plugin organizes a quiz in the chat room.**

There are three types of quiz:
1) Math examples.
   
![image](https://github.com/Stimayk/Quiz/assets/51941742/25f6d479-08d8-47c8-8027-a538549487e4)

3) Questions.
   
![image](https://github.com/Stimayk/Quiz/assets/51941742/54ecf8fa-5646-4631-ac0b-f930bb9db283)

They are taken from the config quiz_question.json
<details>
<summary>quiz_question.json example</summary>

```
{
  "1": {
    "QuestionText": "[История] В каком году произошел взрыв ЧАЭС?",
    "Answer": "1986;1986г"
  },
  "2": {
    "QuestionText": "[Астрономия] Сколько планет в солнечной системе?",
    "Answer": "8;восемь"
  },
  "3": {
    "QuestionText": "[Mirage] Сколько всего ПАЛЬМ на точке А?",
    "Answer": "3"
  },
  "4": {
    "QuestionText": "[Mirage] Сколько ОКОН в А Паласах?",
    "Answer": "7"
  },
  "5": {
    "QuestionText": "[Mirage] Сколько всего графити буквой B, на карте?",
    "Answer": "8"
  }
}
```
</details>

To specify more than one answer for a question, answers must be separated by ;

3) Random numbers.
   
![image](https://github.com/Stimayk/Quiz/assets/51941742/1f502931-b806-40a2-b302-d8bf4f4dfd74)

You have to guess the number that the plugin has guessed.

After a correct answer, the plugin writes a message like this:
(In order for the plugin to give rewards, you need to put the right module, check below.)

![image](https://github.com/Stimayk/Quiz/assets/51941742/6725288d-1611-4e68-b7a5-9730c232e446)

The plugin can combine all 3 types of quizzes and each question will have a random appearance
This can be configured using the quiz.json config.

<details>
<summary>quiz.json example</summary>

```
{
  "ModeExample": true,
  "ModeRn": true,
  "ModeQuestion": true,
  "Time": 30.0,
  "TimeEnd": 15.0,
  "Min": 1,
  "Max": 100,
  "DisplayAnswer": true,
  "MaxAttempts": 3,
  "AnswerTag": "/"
}
```
</details>

+ ModeExample - responsible for the “Mathematical Examples” type of work
+ ModeRn - responsible for “Random numbers” type of quiz.
+ ModeQuestion - responsible for “Questions” type of work
+ Time - time that should pass to start the next quiz
+ TimeEnd - time given for the answer
+ Min - used for ModeRn, the minimum number that the plugin will guess
+ Max - used for ModeRn, the maximum number that the plugin will guess.
+ DisplayAnswer - displays the correct answer after the end of the quiz
+ MaxAttempts - number of attempts to answer in the quiz
+ AnswerTag - where the answer must begin to be counted by the plugin.

Translation support

Available plugins:

+ [QuizShopReward]() - A module that adds a reward in the form of credits for taking a quiz
