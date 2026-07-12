using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskFlowPro.Core.Entities;

namespace TaskFlowPro.Infrastructure.Services
{
    public class AiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _http;
        private readonly ILogger<AiService> _logger;

        // Try these models in order until one works
        private readonly string[] _models = new[]
        {
            "gemini-2.0-flash",
            "gemini-1.5-flash-latest",
            "gemini-1.5-flash",
            "gemini-1.0-pro",
            "gemini-pro"
        };

        private const string BASE =
            "https://generativelanguage.googleapis.com" +
            "/v1beta/models/{MODEL}:generateContent";

        public AiService(
            IConfiguration config,
            ILogger<AiService> logger)
        {
            _apiKey = config["Gemini:ApiKey"] ?? string.Empty;
            _logger = logger;
            _http   = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ── Try each model until one works ───────────────────
        private async Task<string> GenerateAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) ||
                _apiKey == "YOUR_GEMINI_API_KEY_HERE")
            {
                throw new Exception(
                    "Gemini API key is not configured. " +
                    "Please add your key to " +
                    "appsettings.Development.json under " +
                    "Gemini:ApiKey");
            }

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature     = 0.4,
                    maxOutputTokens = 1024,
                    topP            = 0.8,
                    topK            = 40
                }
            };

            var json        = JsonSerializer.Serialize(body);
            Exception? lastError = null;

            // Try each model until one succeeds
            foreach (var model in _models)
            {
                try
                {
                    var url     = BASE.Replace("{MODEL}", model) +
                                  $"?key={_apiKey}";
                    var content = new StringContent(
                        json, Encoding.UTF8, "application/json");

                    _logger.LogInformation(
                        "Trying Gemini model: {Model}", model);

                    var response = await _http.PostAsync(url, content);
                    var raw      = await response.Content
                        .ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Model {Model} failed: {Error}",
                            model, raw);

                        // If quota exceeded try next model
                        if (raw.Contains("RESOURCE_EXHAUSTED") ||
                            raw.Contains("429"))
                        {
                            lastError = new Exception(
                                $"Model {model} quota exceeded");
                            continue;
                        }

                        // If model not found try next model
                        if (raw.Contains("NOT_FOUND") ||
                            raw.Contains("404"))
                        {
                            lastError = new Exception(
                                $"Model {model} not found");
                            continue;
                        }

                        // Other error — throw immediately
                        throw new Exception(
                            $"Gemini API error: {raw}");
                    }

                    // Parse successful response
                    var doc = JsonDocument.Parse(raw);
                    var text = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    _logger.LogInformation(
                        "Success with model: {Model}", model);

                    return text ?? "No response generated.";
                }
                catch (Exception ex) when (
                    ex.Message.Contains("quota") ||
                    ex.Message.Contains("not found"))
                {
                    lastError = ex;
                    continue; // Try next model
                }
            }

            // All models failed
            throw new Exception(
                "All Gemini models failed. " +
                $"Last error: {lastError?.Message}. " +
                "Please check your API key and quota at " +
                "https://aistudio.google.com/app/apikey");
        }

        // ── PUBLIC: Chat with RAG context ────────────────────
        public async Task<string> ChatAsync(
            string userMessage,
            string contextData)
        {
            // Build the full prompt with RAG context injected
            var prompt = $"""
You are TaskFlow Copilot, an AI project management assistant.
You have access to REAL-TIME data from the task management 
system provided below.

IMPORTANT RULES:
1. Answer ONLY based on the data provided below
2. Be specific with numbers and names from the data
3. If asked about overdue tasks, list them by name
4. If asked about workload, compare employees by task count
5. Format your answer clearly with bullet points for lists
6. Be concise — keep answers under 150 words unless asked for detail
7. If the data does not contain the answer, say so clearly

=== REAL-TIME SYSTEM DATA ===
{contextData}
=== END OF DATA ===

USER QUESTION: {userMessage}

YOUR ANSWER (based strictly on the data above):
""";

            try
            {
                return await GenerateAsync(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini API failed. Exact error: {Error}. Falling back to local heuristic analysis.", ex.Message);
                return GetLocalFallbackResponse(userMessage, contextData);
            }
        }

        private string GetLocalFallbackResponse(string message, string context)
        {
            var msg = message.ToLowerInvariant();
            
            if (msg.Contains("overdue"))
            {
                var overdueSection = context.Split("--- OVERDUE TASKS (Need Immediate Attention) ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                if (string.IsNullOrWhiteSpace(overdueSection) || overdueSection.Contains("None"))
                    return "Good news! There are currently no overdue tasks.";
                return $"Here are the currently overdue tasks based on the live data:\n\n{overdueSection}";
            }

            if (msg.Contains("workload") || msg.Contains("team") || msg.Contains("employee"))
            {
                var workloadSection = context.Split("--- TEAM WORKLOAD (All Employees) ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                return $"Here is the current workload for all team members:\n\n{workloadSection}";
            }

            if (msg.Contains("top") || msg.Contains("best") || msg.Contains("perform"))
            {
                var topSection = context.Split("--- TOP PERFORMERS THIS WEEK ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                return $"Here are the top performers for this week:\n\n{topSection}";
            }

            if (msg.Contains("critical") || msg.Contains("urgent") || msg.Contains("priority"))
            {
                var criticalSection = context.Split("--- CRITICAL PRIORITY TASKS (Not Done) ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                return $"These are the critical priority tasks that need attention:\n\n{criticalSection}";
            }

            if (msg.Contains("department"))
            {
                var deptSection = context.Split("--- DEPARTMENT BREAKDOWN ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                return $"Here is the breakdown of tasks by department:\n\n{deptSection}";
            }

            if (msg.Contains("status") || msg.Contains("overview") || msg.Contains("how many"))
            {
                var overviewSection = context.Split("--- TASK STATUS OVERVIEW ---").LastOrDefault()?.Split("---").FirstOrDefault()?.Trim();
                return $"Here is the overall task status overview:\n\n{overviewSection}";
            }

            // Default fallback
            return "I am currently running in **Offline Mode** because a valid API key was not found. \n\n" +
                   "However, I still have access to your live data! Try asking me about:\n" +
                   "• Overdue tasks\n" +
                   "• Team workload\n" +
                   "• Top performers\n" +
                   "• Critical tasks\n" +
                   "• Department breakdown\n" +
                   "• Status overview";
        }

        // ── PUBLIC: Summarize task comments ──────────────────
        public async Task<string> SummarizeTaskAsync(TaskItem task)
        {
            if (!task.Comments.Any())
                return "No comments available to summarize yet.";

            var thread = string.Join("\n", task.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c =>
                    $"{c.User?.Name ?? "Unknown"} " +
                    $"({c.CreatedAt:dd MMM HH:mm}): " +
                    $"{c.Message}"));

            var prompt = $"""
Summarize this task discussion thread in 3-4 sentences.
Identify: (1) current blockers, (2) last decision made, 
(3) next action needed.

Task Title: {task.Title}
Task Status: {task.Status}
Task Priority: {task.Priority}

Comment Thread:
{thread}

Summary:
""";

            try
            {
                return await GenerateAsync(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini API failed for summary. Falling back to local heuristic.");
                var lastComment = task.Comments.OrderBy(c => c.CreatedAt).LastOrDefault();
                if (lastComment != null)
                {
                    return $"*Offline Summary:* The latest update was from {lastComment.User?.Name ?? "Unknown"} saying: '{lastComment.Message}'. Task is currently {task.Status}.";
                }
                return "*Offline Summary:* Discussion is active, task is currently " + task.Status + ". Please view comments directly.";
            }
        }

        // ── PUBLIC: Risk analysis ─────────────────────────────
        public async Task<string> AnalyzeTaskRiskAsync(
            string title, int daysUntilDue,
            int activeTaskCount, string priority,
            string status)
        {
            var prompt = $$"""
Analyze the delay risk for this task.
Respond ONLY with valid JSON, no other text.

Task: {{title}}
Priority: {{priority}}
Status: {{status}}
Days Until Due: {{daysUntilDue}}
Assignee Total Active Tasks: {{activeTaskCount}}

Respond in this exact JSON format:
{"riskLevel":"Low","riskScore":0.2,"reason":"explanation"}

Risk rules:
- riskScore: 0.0 to 1.0 (higher = more risk)
- riskLevel: "Low" if score < 0.4, 
             "Medium" if 0.4-0.7, 
             "High" if > 0.7
- If daysUntilDue < 0: very high risk
- If activeTaskCount > 5: higher risk
- Critical priority adds 0.2 to risk score

JSON response only:
""";

            try
            {
                return await GenerateAsync(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini API failed for risk analysis. Falling back to local heuristic.");
                
                // Fallback risk heuristic
                var score = 0.0;
                if (daysUntilDue < 0) score += 0.5;
                else if (daysUntilDue < 3) score += 0.3;
                
                if (activeTaskCount > 5) score += 0.3;
                if (priority == "Critical") score += 0.2;
                if (priority == "High") score += 0.1;
                
                score = Math.Min(1.0, score);
                
                var level = score < 0.4 ? "Low" : (score < 0.7 ? "Medium" : "High");
                
                return $"{{\"riskLevel\":\"{level}\",\"riskScore\":{score},\"reason\":\"Calculated locally based on deadline and workload due to API unavailability.\"}}";
            }
        }
    }
}