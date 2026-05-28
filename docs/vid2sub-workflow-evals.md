# Vid2Sub Workflow Evals

Run these cases whenever `skills/vid2sub-workflow/SKILL.md` or glossary guidance changes.

## Case 1: Glossary Term Must Win

Input subtitle:

```text
00:00:01.000 --> 00:00:03.000
サイボウズを使います
```

Glossary:

| Japanese | Chinese |
|----------|---------|
| サイボウズ | Cybozu |

Expected:

- Chinese output uses `Cybozu`.
- Start/end timestamps stay unchanged.
- Audit lists `サイボウズ` as matched.

## Case 2: Duplicate Key Warning

Glossary contains the same Japanese key twice.

Expected:

- First entry is used.
- Duplicate appears in `glossary-audit.json` warnings.

## Case 3: Unmatched Term

Subtitle contains a term not present in glossary.

Expected:

- Translation still completes.
- Audit lists the term as unmatched only when Codex identifies it as glossary-relevant.

## Case 4: Segment Preservation

Input has three segments.

Expected:

- Corrected Japanese has three segments.
- Translated Chinese has three segments.
- Every segment keeps the original start/end timestamps.
