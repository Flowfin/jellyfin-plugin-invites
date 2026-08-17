# A document whose pasted statuses still agree

Two pastes, one exiting non-zero and one exiting zero. Both directions are here
on purpose: a check that only ever saw a failing command would agree with a
document by never distinguishing anything.

The first is an absence, which is the shape most of this repository's pastes
have. The token is one nothing in `docs/` carries, and the pathspec keeps this
fixture out of its own answer.

    git grep -n 'zqx-token-absent-from-every-document' -- 'docs/*.md'
    exit=1

The second is a presence, against a heading that is the first line of a document
this repository has carried since before the check existed.

    git grep -c '^# Threat model' -- docs/threat-model.md
    exit=0
