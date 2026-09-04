# Why AI?

This article explains the rationale behind using AI in the generation of this documentation and the measures taken to ensure its accuracy and reliability despite that.

> [!NOTE]
> Information about the use of AI in this article is only subject to the documentation itself. The codebase of **SemPtr** has its own AI-related policies: <https://github.com/fruediger/SemPtr#a-note-on-ai-usage>.

## Rationale

Writing code is can be hard, writing functioning, correct, and secure code can be even harder, but writing a comprehensive and easy-to-understand documentation for it is arguably the hardest task of all. But it's also the most essential part to get right when developing something that might get used by others.

This requires the documentation to grow in the same pace as the codebase itself, and, if the code is ahead or even already completed, the documentation must catch up as quickly as possible to give users the ability to understand and use the code effectively.

To be absolutely honest, **SemPtr** lacks good documentation coverage at the moment, and that is while the codebase could even be considered relatively mature. This gap creates the necessity for a quick and efficient solution to generate high-quality documentation. Hence why AI was employed to help with the creation of the documentation.

Because of the usage of AI, there were some rules and guidelines put in place which include to ensure that all AI-generated content achieves the quality and accuracy goals expected from a good documentation.

## Measures Taken and Guarantees

To ensure the quality and reliability of the AI-generated documentation, several measures have been implemented giving users guarantees about the content:

- **Attribution**: All AI-generated content must be clearly marked as such to ensure the user is aware of its origin and can take it into account when evaluating the content.
- **Human Review**: All AI-generated content must be verified and approved, or corrected by a human author prior to publication to ensure its accuracy and reliability. This can mean that AI is iteratively employed to revise and improve the content under human supervision until the quality goals are met.
- **Blame Assignment**: Responsibility for the content is clearly assigned to human authors that reviewed and approved it. An inaccuracy or error in the documentation is therefore attributable to the human reviewers, not the AI that generated it.
- **Continuous Improvement**: The AI-generated content is subject to ongoing review and improvement. Feedback from users and further human review can and should lead to iterative enhancements.
- **Transparency**: AI usage must be openly disclosed as much as possible (as it is done with this article).
- **Only Documentation, Not Code**: Although this article only discusses the use of AI in the creation of the documentation, it can't hurt to clarify that AI is never used to generate working code for the **SemPtr** project. If you want to know more about this, see: <https://github.com/fruediger/SemPtr#a-note-on-ai-usage>.

## Step back from Using AI

Because of the limitations of AI technology, there was made a recent decision to use less AI in the creation of this documentation moving forward. The reason for that is the enormous effort required to make sure that the AI has a well enough understanding of the codebase and its public API semantics, which is hard to achieve consistently for the complex low-level nature of the project.

This means that while AI may still be used for certain tasks, the primary responsibility for creating and maintaining the documentation will be back in the hands of human authors.
