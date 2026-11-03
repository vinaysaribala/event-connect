using System.ComponentModel.DataAnnotations;

namespace EventConnectService.ViewModels;

public record CancelEventViewModel([MaxLength(1000)] string? Message);
