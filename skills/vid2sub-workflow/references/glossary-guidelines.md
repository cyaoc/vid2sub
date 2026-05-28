# Glossary Guidelines

## Column Mapping

Prefer columns in this order:

- key/source term: Japanese, 日文, 原文, Key, Term
- translation: Chinese, 中文, 訳文, Translation
- notes: Notes, 備考, 备注

If more than one candidate exists, ask the user to confirm.

## Correction And Translation

- Trim glossary keys and translations.
- Empty keys are ignored and must appear in warnings.
- Duplicate keys are ignored after the first occurrence and must appear in warnings.
- Use glossary translations exactly unless notes say otherwise.
- Keep timestamps unchanged during correction and translation.

## Eval Cases

Use small fixed cases for prompt changes:

- glossary term appears once and must be translated exactly
- duplicate glossary key produces an audit warning
- unmatched subtitle term appears in audit
- corrected Japanese and translated Chinese segments preserve start/end timestamps
