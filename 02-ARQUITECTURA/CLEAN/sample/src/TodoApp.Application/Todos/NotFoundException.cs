namespace TodoApp.Application.Todos;

public sealed class NotFoundException(string message) : Exception(message);
