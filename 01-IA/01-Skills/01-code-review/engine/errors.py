class ReviewError(Exception):
    """Fatal, user-actionable error. Surfaces as envelope status=error."""

    def __init__(self, code, message, hint=None):
        super().__init__(message)
        self.code = code
        self.message = message
        self.hint = hint


class NeedsInput(Exception):
    """The engine cannot decide deterministically; the host must ask the user.

    `questions` is a list of AskUserQuestion-shaped dicts. Every option carries
    `args`: the CLI arguments to append when the user picks it.
    """

    def __init__(self, reason, questions):
        super().__init__(reason)
        self.reason = reason
        self.questions = questions
