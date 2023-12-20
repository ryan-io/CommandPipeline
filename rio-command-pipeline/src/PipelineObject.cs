namespace Rio.CommandPipeline {
    /// <summary>
    /// Represents the arguments passed to a command pipeline.
    /// </summary>
    public class PipelineObject {
        /// <summary>
        /// Gets an empty instance of the <see cref="PipelineObject"/> class.
        /// </summary>
        /// <remarks>
        /// The Empty property represents an empty instance of the <see cref="PipelineObject"/> class,
        /// and is often used as a placeholder when a null value is not desired.
        /// </remarks>
        /// <value>
        /// An empty instance of the <see cref="PipelineObject"/> class.
        /// </value>
        public static PipelineObject Empty => _empty;

        /// <summary>
        /// Gets a static reference to the <see cref="PipelineObject"/> object.
        /// </summary>
        /// <remarks>
        /// This property allows for direct manipulation of the <see cref="PipelineObject"/> object,
        /// avoiding the need to create a new instance each time it is accessed.
        /// </remarks>
        /// <returns>A static reference to the <see cref="PipelineObject"/> object.</returns>
        public static ref PipelineObject StaticRef => ref _empty;

        /// <summary>
        /// Gets or sets the current state of the command pipeline.
        /// </summary>
        /// <value>
        /// The current state of the command pipeline.
        /// </value>
        public CommandPipelineState CurrentState { get; set; } = CommandPipelineState.NONE;

        static PipelineObject _empty = new();
    }

    /// <summary>
    /// Represents the different states of a command pipeline.
    /// </summary>
    public enum CommandPipelineState {
        NONE,
        START,
        RUNNING,
        END,
        ERROR,
        FINAL
    }
}
