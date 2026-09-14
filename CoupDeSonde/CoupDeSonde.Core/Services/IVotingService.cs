#nullable enable

using CoupDeSonde.Core.Models;

namespace CoupDeSonde.Core.Services;

/// <summary>
/// Handles the submission and persistence of anonymous votes.
/// </summary>
public interface IVotingService
{
    /// <summary>
    /// Validates and atomically processes a vote submission:
    /// verifies the ballot token, marks it as consumed (single-use),
    /// and persists an anonymous <see cref="VoteRecord"/>.
    /// </summary>
    /// <param name="submission">The voter's submission data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="VoteResult"/> indicating the outcome of the operation.</returns>
    Task<VoteResult> SubmitVoteAsync(VoteSubmission submission, CancellationToken cancellationToken = default);
}
