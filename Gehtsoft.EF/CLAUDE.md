# Gehtsoft.EF — Claude Code Project Notes

## SonarQube

- **URL:** http://192.168.1.25:9010
- **Project key:** EntityFramework
- **Token:** sqp_966744816d8aa6ae17c1a3b39b8e3954cc3873bf

Use the SonarQube Web API with `Authorization: Basic <base64(token:)>` (token as username, empty password) to read reports, issues, and coverage metrics.

## Editing rules

- **Never use `replace_all: true`** when extracting a string literal into a constant. Instead, first create the constant definition, then replace each usage individually or use a targeted `old_string` that includes surrounding context (e.g. the full statement) so the constant definition itself is never matched.
