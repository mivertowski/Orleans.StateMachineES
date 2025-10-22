using Stateless.Reflection;

namespace Orleans.StateMachineES.Models;

/// <summary>
/// Represents serializable state information for a state in a Stateless state machine,
/// including its substates and superstate, for use with Orleans.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OrleansStateInfo"/> class from a Stateless <see cref="StateInfo"/>.
/// </remarks>
/// <param name="stateInfo">The state information to wrap.</param>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Models.OrleansStateInfo")]
public class OrleansStateInfo(StateInfo stateInfo)
{

    /// <summary>The instance or value this state represents.</summary>
    [Id(0)]
    public object UnderlyingState { get; } = stateInfo.UnderlyingState;

    /// <summary>Substates defined for this StateResource.</summary>
    [Id(1)]
    public List<OrleansStateInfo> Substates { get; private set; } = stateInfo.Substates is not null
            ? [.. stateInfo.Substates.Select(s => new OrleansStateInfo(s))]
            : [];

    /// <summary>Superstate defined, if any, for this StateResource.</summary>
    [Id(2)]
    public OrleansStateInfo? Superstate { get; private set; } = stateInfo.Superstate is not null
            ? new OrleansStateInfo(stateInfo.Superstate)
            : null;


    /// <summary>
    /// Returns a string representation of the underlying state.
    /// </summary>
    /// <returns>The string representation of the underlying state or "&lt;null&gt;" if null.</returns>
    public override string ToString()
    {
        return UnderlyingState?.ToString() ?? "<null>";
    }
}
