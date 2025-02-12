using Microsoft.AspNetCore.Mvc;

namespace BffLocalApi;

public class ToDoController : ControllerBase
{
    private readonly ILogger<ToDoController> _logger;

    private static readonly List<ToDo> __data = new List<ToDo>()
    {
        new ToDo { Id = ToDo.NewId(), Date = DateTimeOffset.UtcNow, Name = "Demo ToDo API", User = "bob" },
        new ToDo { Id = ToDo.NewId(), Date = DateTimeOffset.UtcNow.AddHours(1), Name = "Stop Demo", User = "bob" },
        new ToDo { Id = ToDo.NewId(), Date = DateTimeOffset.UtcNow.AddHours(4), Name = "Have Dinner", User = "alice" },
    };

    public ToDoController(ILogger<ToDoController> logger)
    {
        _logger = logger;
    }

    [HttpGet("todos")]
    public IActionResult GetAll()
    {
        _logger.LogInformation("GetAll");

        return Ok(__data.AsEnumerable());
    }

    [HttpGet("todos/{id}")]
    public IActionResult Get(int id)
    {
        var item = __data.FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound();

        _logger.LogInformation("Get {id}", id);
        return Ok(item);
    }

    [HttpPost("todos")]
    public IActionResult Post([FromBody] ToDo model)
    {
        model.Id = ToDo.NewId();
        model.User = $"{User.FindFirst("sub").Value} ({User.FindFirst("name").Value})";

        __data.Add(model);
        _logger.LogInformation("Add {name}", model.Name);

        return Created(Url.Action(nameof(Get), new { id = model.Id }), model);
    }

    [HttpPut("todos/{id}")]
    public IActionResult Put(int id, [FromBody] ToDo model)
    {
        var item = __data.FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound();

        item.Date = model.Date;
        item.Name = model.Name;

        _logger.LogInformation("Update {name}", model.Name);

        return NoContent();
    }

    [HttpDelete("todos/{id}")]
    public IActionResult Delete(int id)
    {
        var item = __data.FirstOrDefault(x => x.Id == id);
        if (item == null) return NotFound();

        __data.Remove(item);
        _logger.LogInformation("Delete {id}", id);

        return NoContent();
    }
}

public class ToDo
{
    static int _nextId = 1;
    public static int NewId()
    {
        return _nextId++;
    }

    public int Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Name { get; set; }
    public string User { get; set; }
}