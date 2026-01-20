using Microsoft.AspNetCore.Mvc;
using testapi1.models;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DialogueController : ControllerBase
    {
        [HttpPost]
        public ActionResult<DialogueResponse> Post([FromBody] DialogueRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.playerText))
                return BadRequest("playerText is required.");

            return Ok(new DialogueResponse
            {
                replyText =
                    $"Echo ✅ Player '{req.playerId}' said '{req.playerText}' to NPC '{req.npcId}' at '{req.inGameTime}'",
                conversationId = Guid.NewGuid().ToString("N")
            });
        }
    }
}
