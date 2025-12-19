SHELL = bash

.PHONY: fmt-check
fmt-check:
	dotnet format --exclude-diagnostics WF0001 --verify-no-changes

.PHONY: fmt
fmt:
	dotnet format --exclude-diagnostics WF0001

.PHONY: test
test:
	dotnet test
