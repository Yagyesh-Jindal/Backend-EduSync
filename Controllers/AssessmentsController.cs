using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssessmentsController : ControllerBase
    {
        private readonly IAssessmentService _assessmentService;
        private readonly ICourseService _courseService;
        private readonly IResultService _resultService;

        public AssessmentsController(
            IAssessmentService assessmentService, 
            ICourseService courseService,
            IResultService resultService)
        {
            _assessmentService = assessmentService;
            _courseService = courseService;
            _resultService = resultService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _assessmentService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id) =>
            Ok(await _assessmentService.GetByIdAsync(id));

        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetByCourseId(Guid courseId)
        {
            try
            {
                // Check if course exists
                var course = await _courseService.GetByIdAsync(courseId);
                if (course == null)
                    return NotFound("Course not found");

                // If user is a student, verify enrollment
                if (User.IsInRole("Student"))
                {
                    var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (studentIdClaim == null)
                        return Unauthorized("Invalid token — user ID missing");

                    var studentId = Guid.Parse(studentIdClaim.Value);
                    
                    // Check if student is enrolled in the course
                    var isEnrolled = await _courseService.EnrollmentExistsAsync(studentId, courseId);
                    if (!isEnrolled)
                    {
                        Console.WriteLine($"Student {studentId} tried to access assessments for course {courseId} without being enrolled");
                        return Ok(new List<Assessment>()); // Return empty list instead of error for security
                    }
                }

                // Get assessments for the course
                var assessments = await _assessmentService.GetByCourseIdAsync(courseId);
                return Ok(assessments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetByCourseId: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error retrieving assessments: {ex.Message}");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Create([FromBody] AssessmentDto dto)
        {
            var assessment = await _assessmentService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = assessment.AssessmentId }, assessment);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AssessmentDto dto)
        {
            try
            {
                // Get the current user ID from the token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("User ID not found in token");
                
                var userId = Guid.Parse(userIdClaim.Value);
                
                // Get the existing assessment
                var existingAssessment = await _assessmentService.GetByIdAsync(id);
                if (existingAssessment == null)
                    return NotFound("Assessment not found");
                
                // Verify questions format
                try
                {
                    if (!string.IsNullOrEmpty(dto.Questions))
                    {
                        JsonDocument.Parse(dto.Questions);
                    }
                }
                catch (JsonException)
                {
                    return BadRequest("Invalid JSON format for questions");
                }
                
                // Update the assessment
                dto.AssessmentId = id; // Ensure ID is set correctly
                var updatedAssessment = await _assessmentService.UpdateAsync(id, dto);
                
                return Ok(updatedAssessment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating assessment: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var assessment = await _assessmentService.GetByIdAsync(id);
                if (assessment == null)
                    return NotFound("Assessment not found");
                
                await _assessmentService.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting assessment: {ex.Message}");
            }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitAssessment(Guid id, [FromBody] ResultDto submission)
        {
            try
            {
                Console.WriteLine($"Received submission for assessment {id}");
                Console.WriteLine($"Submission data: {JsonSerializer.Serialize(submission)}");
                
                // Get the current user ID from the token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("User ID not found in token");
                
                var userId = Guid.Parse(userIdClaim.Value);
                Console.WriteLine($"User ID: {userId}");
                
                // Get the assessment
                var assessment = await _assessmentService.GetByIdAsync(id);
                if (assessment == null)
                {
                    Console.WriteLine($"Assessment {id} not found");
                    return NotFound("Assessment not found");
                }

                // Check if answers were submitted
                if (submission.SubmittedAnswers == null || !submission.SubmittedAnswers.Any())
                {
                    Console.WriteLine("No answers were submitted");
                    return BadRequest("No answers were submitted");
                }

                // Parse questions from assessment
                var questions = new List<dynamic>();
                try
                {
                    if (string.IsNullOrEmpty(assessment.Questions))
                    {
                        Console.WriteLine("Assessment has no questions");
                        return BadRequest("Assessment has no questions");
                    }
                    
                    Console.WriteLine($"Raw questions JSON: {assessment.Questions}");
                    questions = JsonSerializer.Deserialize<List<dynamic>>(assessment.Questions);
                    Console.WriteLine($"Parsed {questions.Count} questions from assessment");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing questions: {ex.Message}");
                    return BadRequest($"Invalid question format: {ex.Message}");
                }

                // Process the submission using the ResultService
                submission.AssessmentId = id;
                submission.UserId = userId;
                
                var result = await _resultService.CalculateResultAsync(submission);

                // Count the correct answers
                int correctAnswersCount = 0;
                int totalQuestions = questions.Count;
                
                // Create a dictionary for easier lookup of submitted answers
                var submittedAnswersDict = submission.SubmittedAnswers
                    .ToDictionary(a => a.QuestionId, a => a.SelectedAnswerId);
                
                // Process each question to count correct answers
                foreach (var questionObj in questions)
                {
                    try
                    {
                        // Convert dynamic to JsonElement for safer property access
                        var question = (JsonElement)questionObj;
                        
                        // Get the question ID
                        string questionId = "";
                        if (question.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                        {
                            questionId = idElement.GetString();
                        }
                        else
                        {
                            continue;
                        }
                        
                        // Get the correct option
                        string correctOption = "";
                        if (question.TryGetProperty("correctOption", out var correctElement))
                        {
                            // Handle both string and number formats
                            if (correctElement.ValueKind == JsonValueKind.String)
                            {
                                correctOption = correctElement.GetString();
                            }
                            else if (correctElement.ValueKind == JsonValueKind.Number)
                            {
                                correctOption = correctElement.GetInt32().ToString();
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                        
                        // Check if the user submitted an answer for this question
                        if (submittedAnswersDict.TryGetValue(questionId, out string selectedAnswer))
                        {
                            // Check if the submitted answer is correct
                            if (selectedAnswer == correctOption)
                            {
                                correctAnswersCount++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                return Ok(new { 
                    message = "Assessment submitted successfully",
                    score = result.Score,
                    maxScore = assessment.MaxScore,
                    percentage = result.Percentage,
                    isPassed = result.IsPassed,
                    attemptDate = result.AttemptDate,
                    correctAnswers = correctAnswersCount,
                    totalQuestions = totalQuestions
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting assessment: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error submitting assessment: {ex.Message}");
            }
        }

        // Helper method to count correct answers
        private int CountCorrectAnswers(Dictionary<string, string> answers, List<dynamic> questions)
        {
            int correctAnswers = 0;
            Console.WriteLine("Counting correct answers:");
            
            foreach (var questionObj in questions)
            {
                try
                {
                    // Convert dynamic to JsonElement for safer property access
                    var question = (JsonElement)questionObj;
                    
                    // Get the question ID
                    string questionId = "";
                    if (question.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                    {
                        questionId = idElement.GetString();
                    }
                    else
                    {
                        Console.WriteLine("  Question missing ID property");
                        continue;
                    }
                    
                    // Get the correct option
                    string correctOption = "";
                    if (question.TryGetProperty("correctOption", out var correctElement))
                    {
                        // Handle both string and number formats
                        if (correctElement.ValueKind == JsonValueKind.String)
                        {
                            correctOption = correctElement.GetString();
                            Console.WriteLine($"  Question {questionId} - Correct option (string): '{correctOption}'");
                        }
                        else if (correctElement.ValueKind == JsonValueKind.Number)
                        {
                            correctOption = correctElement.GetInt32().ToString();
                            Console.WriteLine($"  Question {questionId} - Correct option (number): '{correctOption}'");
                        }
                        else
                        {
                            Console.WriteLine($"  Question {questionId} has unsupported correctOption type: {correctElement.ValueKind}");
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Question {questionId} missing correctOption property");
                        continue;
                    }
                    
                    // Check if the user's answer matches the correct option
                    if (answers.TryGetValue(questionId, out string userAnswer))
                    {
                        Console.WriteLine($"  User answer for {questionId}: '{userAnswer}'");
                        
                        // Try different comparison methods to debug the issue
                        bool exactMatch = userAnswer == correctOption;
                        bool trimmedMatch = userAnswer.Trim() == correctOption.Trim();
                        bool ignoreCaseMatch = string.Equals(userAnswer.Trim(), correctOption.Trim(), StringComparison.OrdinalIgnoreCase);
                        
                        Console.WriteLine($"  Comparison results - Exact: {exactMatch}, Trimmed: {trimmedMatch}, IgnoreCase: {ignoreCaseMatch}");
                        
                        // Use exact match for strictest comparison
                        if (exactMatch)
                        {
                            Console.WriteLine($"  CORRECT! User answer '{userAnswer}' matches correct option '{correctOption}'");
                            correctAnswers++;
                        }
                        else
                        {
                            Console.WriteLine($"  INCORRECT. Expected: '{correctOption}', Got: '{userAnswer}'");
                            Console.WriteLine($"  Character codes - Expected: {string.Join(",", correctOption.Select(c => ((int)c).ToString()))}");
                            Console.WriteLine($"  Character codes - Actual: {string.Join(",", userAnswer.Select(c => ((int)c).ToString()))}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  No answer provided for question {questionId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error processing question: {ex.Message}");
                    // Skip questions with invalid format
                    continue;
                }
            }
            
            Console.WriteLine($"Total correct answers: {correctAnswers}");
            return correctAnswers;
        }

        private int CalculateScore(Dictionary<string, string> answers, List<dynamic> questions, int maxScore)
        {
            if (questions == null || questions.Count == 0)
                return 0;

            int correctAnswers = CountCorrectAnswers(answers, questions);
            int totalQuestions = questions.Count;

            // Calculate score as a percentage of maxScore
            double scorePercentage = (double)correctAnswers / totalQuestions;
            int finalScore = (int)Math.Round(scorePercentage * maxScore);

            return finalScore;
        }
    }
} 