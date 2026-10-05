# Agent Instructions

- Before starting development, read `docs/PROJECT_CONTEXT.md` first and use it as the project map instead of scanning the whole repository by default.
- After every development task, update `docs/PROJECT_CONTEXT.md` when behavior, schema, routes, workflows, project structure, or conventions change.
- When a change modifies the Entity Framework database schema, automatically add the corresponding migration file before finishing the task.
- Ask for user approval before running any build or compilation check such as `dotnet build`.
- Never push commits to a remote unless the user explicitly instructs you to push. After development, keep changes local until that instruction is given.
- Never create a local Docker image on a server or run a server-side recreate/restart operation without the user's explicit approval for that operation. After pushing to `main`, do not monitor the GitHub workflow or check whether Docker images have finished building; leave image verification, server-side image pulls, and service restarts to the user unless the user explicitly asks you to handle a specific step. The normal release sequence is: commit the changes, push to `main`, let the GitHub workflow build the Docker images, then the user verifies/pulls the updated images and restarts the services.
