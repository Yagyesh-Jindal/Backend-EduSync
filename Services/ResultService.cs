using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace EduSyncAPI.Services
{
    public class ResultService : IResultService
    {
        private readonly EduSyncDbContext _context;
        private readonly int PASS_PERCENTAGE = 70; // Configurable pass percentage

        public ResultService(EduSyncDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Result>> GetAllAsync() =>
            await _context.Results.ToListAsync();

        public async Task<Result> GetByIdAsync(Guid id) =>
            await _context.Results.FindAsync(id);

        public async Task<Result> CreateAsync(ResultDto dto)
        {
            // Calculate the result based on submitted answers
            return await CalculateResultAsync(dto);
        }

        public async Task<Result> CalculateResultAsync(ResultDto dto)
        {
            // Get the assessment with all details
            var assessment = await _context.Assessments
                .FindAsync(dto.AssessmentId);
            
            if (assessment == null)
                throw new InvalidOperationException("Assessment not found");

            try
            {
                // Check if the user has submitted any answers
                if (dto.SubmittedAnswers == null || !dto.SubmittedAnswers.Any())
                {
                    throw new InvalidOperationException("No answers were submitted");
                }

                Console.WriteLine($"Processing assessment with questions: {assessment.Questions}");
                Console.WriteLine($"Submitted answers count: {dto.SubmittedAnswers.Count}");

                // Deserialize the questions from the JSON string
                List<dynamic> questions;
                try
                {
                    questions = JsonSerializer.Deserialize<List<dynamic>>(assessment.Questions);
                    
                    if (questions == null || !questions.Any())
                        throw new InvalidOperationException("Assessment has no questions");
                    
                    // Log each question for debugging
                    for (int i = 0; i < questions.Count; i++)
                    {
                        var q = (JsonElement)questions[i];
                        Console.WriteLine($"Question {i+1} structure:");
                        Console.WriteLine($"  Raw: {JsonSerializer.Serialize(q)}");
                        
                        if (q.TryGetProperty("id", out var idProp))
                            Console.WriteLine($"  id: {idProp}");
                        
                        if (q.TryGetProperty("text", out var textProp))
                            Console.WriteLine($"  text: {textProp}");
                        
                        if (q.TryGetProperty("options", out var optionsProp))
                            Console.WriteLine($"  options: {JsonSerializer.Serialize(optionsProp)}");
                        
                        if (q.TryGetProperty("correctOption", out var correctProp))
                            Console.WriteLine($"  correctOption: {correctProp} (Type: {correctProp.ValueKind})");
                        else
                            Console.WriteLine("  correctOption: NOT FOUND");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing questions: {ex.Message}");
                    throw new InvalidOperationException($"Invalid question format: {ex.Message}");
                }

                // Calculate the score based on submitted answers
                int totalScore = 0;
                int correctAnswersCount = 0;
                int totalQuestions = questions.Count;
                int maxPossibleScore = assessment.MaxScore;

                // Log submitted answers for debugging
                foreach (var answer in dto.SubmittedAnswers)
                {
                    Console.WriteLine($"Submitted answer - QuestionId: {answer.QuestionId}, SelectedAnswerId: {answer.SelectedAnswerId}");
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
                            Console.WriteLine($"Processing question with ID: {questionId}");
                        }
                        else
                        {
                            Console.WriteLine("Question missing ID property - skipping");
                            continue;
                        }
                        
                        // Get the question points
                        int points = 1; // Default value
                        if (question.TryGetProperty("points", out var pointsElement) && pointsElement.ValueKind == JsonValueKind.Number)
                        {
                            points = pointsElement.GetInt32();
                            Console.WriteLine($"Question points: {points}");
                        }
                        
                        // Get the correct option
                        string correctOption = "";
                        if (question.TryGetProperty("correctOption", out var correctElement))
                        {
                            // Handle both string and number formats
                            if (correctElement.ValueKind == JsonValueKind.String)
                            {
                                correctOption = correctElement.GetString();
                                Console.WriteLine($"Correct option (string): '{correctOption}'");
                            }
                            else if (correctElement.ValueKind == JsonValueKind.Number)
                            {
                                correctOption = correctElement.GetInt32().ToString();
                                Console.WriteLine($"Correct option (number): '{correctOption}'");
                            }
                            else
                            {
                                Console.WriteLine($"Unsupported correctOption type: {correctElement.ValueKind} - skipping");
                                continue;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Question missing correctOption property - skipping");
                            continue;
                        }
                        
                        // Check if the user submitted an answer for this question
                        if (submittedAnswersDict.TryGetValue(questionId, out var userAnswer))
                        {
                            Console.WriteLine($"User answer for question {questionId}: '{userAnswer}'");
                            Console.WriteLine($"Correct answer for question {questionId}: '{correctOption}'");
                            
                            // Check if the submitted answer is correct
                            if (userAnswer == correctOption)
                            {
                                Console.WriteLine($"CORRECT! Adding {points} points");
                                totalScore += points;
                                correctAnswersCount++;
                            }
                            else
                            {
                                Console.WriteLine("INCORRECT answer");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No answer submitted for question {questionId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing question: {ex.Message}");
                        // Skip questions with invalid format
                        continue;
                    }
                }

                // Calculate percentage
                double percentage = maxPossibleScore > 0 
                    ? Math.Round((double)totalScore / maxPossibleScore * 100, 2) 
                    : 0;

                // Determine if passed (e.g., 70% or higher)
                bool isPassed = percentage >= PASS_PERCENTAGE;

                Console.WriteLine($"Final result: Score={totalScore}, Percentage={percentage}%, Passed={isPassed}");

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
                
                return result;
            }
            catch (Exception ex)
            {
                // Log the exception but don't expose internal details
                Console.WriteLine($"Error calculating result: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
