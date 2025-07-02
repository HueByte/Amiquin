# GitHub Actions Workflow Organization

This document explains the organization and purpose of each GitHub Actions workflow in this repository.

## Workflow Files Overview

### 🚀 **Core CI/CD Workflows**

#### 1. `amiquin-ci.yml` - .NET CI

**Purpose**: Core .NET build, test, and code quality checks
**Triggers**:

- Push to main (source changes)
- Pull requests to main/dev (source changes)

**Jobs**:

- ✅ Build solution with .NET 9.0
- ✅ Run unit tests with coverage (if test projects exist)
- ✅ Code formatting verification
- ✅ Artifact uploads (test results, coverage)

---

#### 2. `create-release.yml` - Manual Release Management

**Purpose**: Tag-triggered release builds and artifact distribution
**Triggers**:

- Manual tag creation (v*.*.*)
- Pull requests to main (validation only)

**Jobs**:

- ✅ PR validation (build, test, format, security)
- ✅ Release artifact building (Linux, Windows, macOS) on tag creation
- ✅ Multi-platform binary distribution

**Manual Process**: Developer creates git tag to trigger release build
**Release Artifacts**: Self-contained binaries for Linux, Windows, and macOS
**Full Control**: No automatic version detection or tag creation

---

### 🔒 **Security & Quality Workflows**

#### 3. `amiquin-security.yml` - Security Scanning

**Purpose**: Automated security vulnerability scanning
**Triggers**:

- Weekly schedule (Mondays at 2 AM UTC)
- Manual workflow dispatch

**Jobs**:

- ✅ .NET security audit
- ✅ CodeQL analysis
- ✅ Dependency vulnerability checks

---

### 🐳 **Infrastructure Workflows**

#### 4. `docker-build.yml` - Container Build

**Purpose**: Docker image building and container security
**Triggers**:

- Push to main (source/Amiquin.Bot/dockerfile changes)
- Pull requests (source/Amiquin.Bot/dockerfile changes)

**Jobs**:

- ✅ Build Docker images
- ✅ Container testing
- ✅ Trivy security scanning
- ✅ Push to container registry

---

### 📝 **Documentation & Linting**

#### 5. `markdown-lint.yml` - Documentation Quality

**Purpose**: Markdown file linting and formatting
**Triggers**:

- Push to main (*.md changes)
- Pull requests (*.md changes)

**Jobs**:

- ✅ Markdown linting with markdownlint-cli
- ✅ Auto-configuration setup

---

#### 6. `deploy-docs.yml` - Documentation Deployment

**Purpose**: Build and deploy project documentation
**Triggers**:

- Push to main
- Manual workflow dispatch

**Jobs**:

- ✅ DocFX documentation generation
- ✅ API documentation from XML comments
- ✅ GitHub Pages deployment

---

### 🔧 **Performance & Testing**

#### 7. `amiquin-performance.yml` - Performance Testing

**Purpose**: Performance benchmarking and monitoring
**Triggers**:

- Scheduled runs
- Manual dispatch

**Jobs**:

- ✅ Performance benchmarks
- ✅ Performance regression detection

---

### 🐛 **Development & Debugging**

#### 8. `simple-test.yml` - Quick Tests

**Purpose**: Fast development testing
**Triggers**: Various development events

---

#### 9. `debug-triggers.yml` - Workflow Debugging

**Purpose**: Debug and troubleshoot workflow triggers
**Triggers**: Development and debugging scenarios

---

## Workflow Separation Benefits

### ✅ **Improved Organization**

- Each workflow has a single, clear responsibility
- Easier to maintain and debug individual components
- Parallel execution reduces overall CI/CD time

### ✅ **Targeted Triggers**

- Workflows only run when relevant files change
- Reduced resource usage and faster feedback
- Security scans run on schedule vs. every commit

### ✅ **Independent Scaling**

- Can modify one workflow without affecting others
- Different permission requirements per workflow
- Easier to add new workflows for specific needs

### ✅ **Clear Failure Isolation**

- Failed Docker build doesn't block .NET tests
- Documentation issues don't stop releases
- Security warnings don't break development flow

## Best Practices Implemented

1. **Token Configuration**: All workflows use proper `${{ secrets.GITHUB_TOKEN }}`
2. **Consistent .NET Version**: All .NET workflows use 9.0.x
3. **Path-based Triggers**: Workflows only run when relevant files change
4. **Proper Dependencies**: Security scans run after successful builds
5. **Artifact Management**: Test results and coverage properly uploaded
6. **Error Handling**: Appropriate continue-on-error for non-critical steps

## Usage

- **For .NET Development**: `amiquin-ci.yml` provides core CI
- **For Releases**: `create-release.yml` handles automated versioning
- **For Security**: `amiquin-security.yml` runs weekly scans
- **For Containers**: `docker-build.yml` handles Docker workflows
- **For Documentation**: Use `markdown-lint.yml` and `deploy-docs.yml`

Each workflow can be triggered independently and provides specific feedback for its domain.
