# GitHub Actions self-hosted runner validation

CxCell keeps the normal CI on GitHub-hosted Windows runners and adds a manual self-hosted workflow as a fallback/validation path.

## Important for this repository

CxCell is currently a **public** repository. GitHub's standard hosted runners are free and unlimited for public repositories, so the self-hosted runner is not needed to save hosted-runner minutes here.

The self-hosted path is still useful to validate the same model needed by future private repositories, to reproduce Windows-only behavior locally, or to avoid depending on GitHub-hosted compute availability.

Do **not** attach the self-hosted runner to pull-request workflows for this public repository. A self-hosted machine has access to your local environment, and running untrusted fork PR code on it is unsafe.

The CxCell self-hosted workflow is therefore **manual-only** via `workflow_dispatch`.

## 1. Add the repository runner

On GitHub:

1. Open the CxCell repository.
2. Open **Settings**.
3. Open **Actions → Runners**.
4. Click **New self-hosted runner**.
5. Select **Windows / x64**.
6. Follow the download and extraction commands GitHub shows.

Use a dedicated directory such as:

`C:\actions-runner\cxcell`

GitHub generates a registration token on that page. The token is time-limited, so use the command shown by GitHub instead of storing the token in this repository.

When running `config.cmd`, add the custom label:

`cxcell-local`

The runner used by CxCell must have these labels:

- `self-hosted`
- `windows`
- `x64`
- `cxcell-local`

## 2. Install as a Windows service

Run the runner setup from an **Administrator PowerShell** if you want it to run without keeping a terminal window open.

During `config.cmd`, answer yes when GitHub asks whether to run the runner as a service.

After registration, GitHub should show the runner as **Idle** under:

**Settings → Actions → Runners**

## 3. Trigger the validation workflow

Open:

**Actions → ci-self-hosted → Run workflow**

Choose the branch:

`feature/self-hosted-runner-validation`

Keep **Upload artifact** enabled for the first validation.

The job should be assigned to the local Windows runner and execute:

1. checkout + clean workspace
2. runner diagnostics
3. .NET 8 setup
4. restore
5. Release build
6. unit tests
7. self-contained Windows x64 publish
8. optional artifact upload

A successful run proves that GitHub can orchestrate CxCell CI on the local runner without using GitHub-hosted compute.

## 4. Reboot validation

After the first successful run:

1. Reboot Windows.
2. Do not manually start `run.cmd`.
3. Check **Settings → Actions → Runners** and confirm the runner returns to **Idle**.
4. Trigger `ci-self-hosted` again.
5. Confirm the second run succeeds.

This validates the Windows-service setup, which is the equivalent of the persistent local-runner setup used in the GitLab flow.

## 5. Normal operating model

Recommended:

- normal public-repository CI: `ci.yml` on `windows-latest`
- local fallback / parity validation: `ci-self-hosted.yml`
- release: `release.yml` on GitHub-hosted Windows

For a future **private** repository with limited hosted-runner minutes, the same self-hosted workflow can be made the primary CI path or invoked whenever hosted minutes are unavailable.

GitHub does not provide a clean automatic "hosted minutes exhausted → transparently retry on self-hosted" routing rule. Keep the self-hosted workflow explicit rather than duplicating jobs or relying on fragile failure detection.
