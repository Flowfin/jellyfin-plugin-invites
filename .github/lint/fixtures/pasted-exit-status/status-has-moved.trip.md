# A document whose pasted status has moved

    git grep -c '^# Threat model' -- docs/threat-model.md
    exit=1

That is the mistake this check exists for, and it is one character. The command
finds the heading and exits zero; the paste says one. Somebody wrote the paste on
a day when the command found nothing, the tree moved, and the sentence that
rested on it never had to change to become false.

The paste below is correct, and it is here so the check is made to distinguish
rather than to red every paste in a file that has one wrong.

    git grep -n 'zqx-token-absent-from-every-document' -- 'docs/*.md'
    exit=1
