using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResultsController : ControllerBase
    {
        private readonly IResultService _resultService;
        private readonly EduSyncDbContext _context;
        private readonly int PASS_PERCENTAGE = 70; // Configurable pass percentage

        public ResultsController(IResultService resultService, EduSyncDbContext context)
        {
            _resultService = resultService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _resultService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id) =>
            Ok(await _resultService.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ResultDto dto)
        {
            var result = await _resultService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.ResultId }, result);
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateResult([FromBody] ResultDto dto)
        {
            try
            {
                // Get the assessment with all details
                var assessment = await _context.Assessments
                    .FindAsync(dto.AssessmentId);
                
                if (assessment == null)
                    return NotFound("Assessment not found");

                // Deserialize the questions from the JSON string
                List<dynamic> questions;
                try
                {
                    questions = JsonSerializer.Deserialize<List<dynamic>>(assessment.Questions);
                    
                    if (questions == null || !questions.Any())
                        return BadRequest("Assessment has no questions");
                }
                catch (JsonException ex)
                {
                    return BadRequest($"Invalid question format: {ex.Message}");
                }

                // Calculate the score based on submitted answers
                int totalScore = 0;
                int correctAnswersCount = 0;
                int totalQuestions = questions.Count;
                int maxPossibleScore = assessment.MaxScore;

                // Check if the user has submitted any answers
                if (dto.SubmittedAnswers == null || !dto.SubmittedAnswers.Any())
                {
                    return BadRequest("No answers were submitted");
                }

                // Create a dictionary for easier lookup of submitted answers
                var submittedAnswersDict = dto.SubmittedAnswers
                    .ToDictionary(a => a.QuestionId, a => a.SelectedAnswerId);

                // Process each question
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
                            // Skip questions with missing IDs
                            continue;
                        }
                        
                        // Get the question points
                        int points = 1; // Default value
                        if (question.TryGetProperty("points", out var pointsElement) && pointsElement.ValueKind == JsonValueKind.Number)
                        {
                            points = pointsElement.GetInt32();
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
                                // Skip questions with invalid correctOption format
                                continue;
                            }
                        }
                        else
                        {
                            // Skip questions without correctOption
                            continue;
                        }
                        
                        // Check if the user submitted an answer for this question
                        if (submittedAnswersDict.TryGetValue(questionId, out string selectedAnswer))
                        {
                            // Check if the submitted answer is correct
                            if (selectedAnswer == correctOption)
                            {
                                totalScore += points;
                                correctAnswersCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and skip questions with processing errors
                        Console.WriteLine($"Error processing question: {ex.Message}");
                        continue;
                    }
                }

                // Calculate percentage
                double percentage = maxPossibleScore > 0 
                    ? Math.Round((double)totalScore / maxPossibleScore * 100, 2) 
                    : 0;

                // Determine if passed (e.g., 70% or higher)
                bool isPassed = percentage >= PASS_PERCENTAGE;

                // Create and save the result
                var result = new Result
                {
                    ResultId = Guid.NewGuid(),
                    AssessmentId = dto.AssessmentId,
                    UserId = dto.UserId,
                    Score = totalScore,
                    Percentage = percentage,
                    IsPassed = isPassed,
                    AttemptDate = DateTime.UtcNow
                };

                _context.Results.Add(result);
                await _context.SaveChangesAsync();
                
                // Return a detailed response with all the calculated values
                return Ok(new {
                    resultId = result.ResultId,
                    assessmentId = result.AssessmentId,
                    userId = result.UserId,
                    score = result.Score,
                    percentage = result.Percentage,
                    isPassed = result.IsPassed,
                    attemptDate = result.AttemptDate,
                    correctAnswers = correctAnswersCount,
                    totalQuestions = totalQuestions,
                    message = result.IsPassed 
                        ? "Congratulations! You passed the assessment." 
                        : "You did not pass the assessment. Please review the material and try again."
                });
            }
            catch (Exception ex)
            {
                // Log the exception and return a generic error message
                Console.WriteLine($"Error calculating result: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, "An error occurred while calculating the assessment result.");
            }
        }
    }
}
