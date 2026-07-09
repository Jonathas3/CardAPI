namespace Cards.Application.Dtos;

public class DeleteResultDto
{
    public Guid Id { get; set; }
    public bool Deleted { get; set; }
    public string Message { get; set; } = string.Empty;
}
