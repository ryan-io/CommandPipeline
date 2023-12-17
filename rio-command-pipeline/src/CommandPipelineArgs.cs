namespace Rio.CommandPipeline {
    public class CommandPipelineArgs {
        public static CommandPipelineArgs Empty => _empty;

        public static ref CommandPipelineArgs StaticRef => ref _empty;

        public CommandPipelineState CurrentState { get; set; } = CommandPipelineState.NONE;

        static CommandPipelineArgs _empty = new();
    }

    public enum CommandPipelineState {
        NONE,
        START,
        RUNNING,
        END,
        ERROR,
        FINAL
    }
}
