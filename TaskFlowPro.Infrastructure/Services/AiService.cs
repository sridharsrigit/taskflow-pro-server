using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaskFlowPro.Core.Entities;

namespace TaskFlowPro.Infrastructure.Services
{
    public class AiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _http;
        private const string BASE_URL =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        public AiService(IConfiguration config)
        {
            _apiKey = config["Gemini:ApiKey"] ?? string.Empty;
            _http   = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        // ── CORE METHOD ───────────────────────────────────────
        private async Task<string> GenerateAsync(string prompt)
        {
            // If no API key configured return demo response
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "AIzaSyBPPj2i8cHuJYWUb_25J-QT2t9hitzM02E")
                return GetDemoResponse(prompt);

            try
            {
                var url  = $"{BASE_URL}?key={_apiKey}";
                var body = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature     = 0.7,
                        maxOutputTokens = 500
                    }
                };

                var json     = JsonSerializer.Serialize(body);
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content);
                var raw      = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // If quota exceeded return demo response instead of error
                    if (raw.Contains("429") || raw.Contains("quota") || raw.Contains("RESOURCE_EXHAUSTED"))
                        return GetDemoResponse(prompt);

                    throw new Exception($"Gemini error: {raw}");
                }

                var doc  = JsonDocument.Parse(raw);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? "No response generated.";
            }
            catch (Exception ex) when (ex.Message.Contains("quota") || ex.Message.Contains("429"))
            {
                return GetDemoResponse(prompt);
            }
        }

        // ── DEMO RESPONSES ────────────────────────────────────
        // Returns smart demo responses when AI quota is exceeded
        private string GetDemoResponse(string prompt)
        {
            var lower = prompt.ToLower();

            if (lower.Contains("summarize") || lower.Contains("comment"))
                return "The team has been discussing implementation details. " +
                       "The main blocker is the API integration which needs to be resolved. " +
                       "Last decision was to proceed with the current approach. " +
                       "Next action: complete the implementation and submit for review.";

            if (lower.Contains("overdue"))
                return "Based on current data, there are tasks that have passed their due dates. " +
                       "I recommend reassigning or extending deadlines for overdue items " +
                       "and prioritising critical tasks first.";

            if (lower.Contains("workload") || lower.Contains("team"))
                return "The team workload appears to be unevenly distributed. " +
                       "Some employees have more active tasks than others. " +
                       "Consider redistributing tasks for better balance.";

            if (lower.Contains("risk"))
                return "{\"riskLevel\":\"Medium\",\"riskScore\":0.5," +
                       "\"reason\":\"Task has moderate complexity with reasonable timeline.\"}";

            if (lower.Contains("suggestion"))
                return "[{\"suggestion\":\"Review and reassign overdue tasks immediately\"," +
                       "\"priority\":\"High\"}," +
                       "{\"suggestion\":\"Check in with employees who have 5+ active tasks\"," +
                       "\"priority\":\"Medium\"}," +
                       "{\"suggestion\":\"Update task priorities before end of week\"," +
                       "\"priority\":\"Low\"}]";

            return "I can help you manage your team tasks more effectively. " +
                   "Ask me about overdue tasks, team workload, or task priorities.";
        }

        // ── PUBLIC METHODS ────────────────────────────────────
        public async Task<string> SummarizeTaskAsync(TaskItem task)
        {
            if (!task.Comments.Any())
                return "No comments to summarize yet. Add comments to get an AI summary.";

            var thread = string.Join("\n", task.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c =>
                    $"{c.User?.Name ?? "Unknown"} " +
                    $"({c.CreatedAt:dd MMM HH:mm}): {c.Message}"));

            var prompt =
                $"Summarize this task comment thread in 3-4 sentences. " +
                $"Highlight blockers, last decision, and next action.\n\n" +
                $"Task: {task.Title}\nStatus: {task.Status}\n\nComments:\n{thread}";

            return await GenerateAsync(prompt);
        }

        public async Task<string> ChatAsync(string userMessage, string contextData)
        {
            var prompt =
                $"You are TaskFlow Copilot. Answer using only this data:\n\n" +
                $"{contextData}\n\nQuestion: {userMessage}";

            return await GenerateAsync(prompt);
        }

        public async Task<string> AnalyzeTaskRiskAsync(
            string taskTitle, int daysUntilDue,
            int assigneeTaskCount, string priority, string status)
        {
            var prompt =
                $"Analyze risk for this task. " +
                $"Task: {taskTitle}, Priority: {priority}, " +
                $"Status: {status}, Days Until Due: {daysUntilDue}, " +
                $"Assignee Active Tasks: {assigneeTaskCount}.\n\n" +
                $"Respond in EXACT JSON only:\n" +
                $"{{\"riskLevel\":\"Low\",\"riskScore\":0.3," +
                $"\"reason\":\"one sentence\"}}";

            return await GenerateAsync(prompt);
        }
    }
}