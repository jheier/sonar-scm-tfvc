namespace SonarSource.TfsAnnotate
{
    using Microsoft.TeamFoundation.VersionControl.Client;

    internal interface IAnnotatedFile
    {
        bool IsBinary();

        int Lines();

        string Data(int line);

        AnnotationState State(int line);

        Changeset Changeset(int line);
    }
}