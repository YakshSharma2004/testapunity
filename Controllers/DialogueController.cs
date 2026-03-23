using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Services;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DialogueController : ControllerBase
    {
        private readonly ITextNormalizer _normalizer;
        private readonly ILogger<DialogueController> _logger;
        private readonly IEmbeddingService _embeddingService;
        public DialogueController(ITextNormalizer normalizer, ILogger<DialogueController> logger, IEmbeddingService embeddingService)
        {
            _normalizer = normalizer;
            _logger = logger;
            _embeddingService = embeddingService;
        }
        [HttpPost("normalise")]
        public ActionResult<DialogueResponse> Post([FromBody] DialogueRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.playerText))
                return BadRequest("playerText is required.");
            var normalisedText = _normalizer.NormalizeForMatch(req.playerText);
            _logger.LogDebug("Normalized text for player {PlayerId}: {NormalizedText}", req.playerId, normalisedText);
            return Ok(new DialogueResponse
            {
                replyText =
                    $"Echo Player '{req.playerId}' said '{normalisedText}' to NPC '{req.npcId}' at '{req.inGameTime}'",
                conversationId = Guid.NewGuid().ToString("N")
            });
        }

        [HttpPost("embed")]
        public async Task<IActionResult> Embed([FromBody] DialogueRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.playerText))
                return BadRequest("playerText is required.");

            var vec = await _embeddingService.EmbedAsync(req.playerText, ct);

            return Ok(new
            {
                dims = vec.Length,
                preview = vec.Take(8).ToArray()
            });
        }
    }
    }
