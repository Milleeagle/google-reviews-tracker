using GoogleReviews.Core.Models;

namespace GoogleReviews.Core.Interfaces;

public interface IGoogleDocsService
{
    Task ReportChangesAsync(List<ReviewChange> changes);
}