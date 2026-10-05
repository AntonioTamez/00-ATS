# Lens: security (`"lens": "security"`)

Find vulnerabilities introduced by the added lines. Think like an attacker: what input can they
control, where does it flow, and what does it reach?

Look for:
1. **Injection**: SQL/NoSQL, OS command, LDAP, template, path traversal, SSRF, open redirect,
   unsafe deserialization, regex DoS on user input, header/log injection.
2. **AuthN/AuthZ**: new endpoint/handler/job without authentication or authorization, missing
   ownership/tenant check (IDOR), trusting client-supplied roles/ids, privilege checks done on
   the client only, weak session/token handling.
3. **Secrets and data exposure**: credentials or tokens in code/config/logs/URLs, sensitive data in
   error messages or responses, PII logged, overly broad CORS, debug endpoints.
4. **Crypto**: homemade crypto, weak algorithms/modes, static IV/salt, predictable randomness for
   security purposes, disabled certificate validation, insecure password storage.
5. **Supply chain and CI**: new dependencies from untrusted sources, unpinned scripts executed in
   pipelines, workflows that run untrusted code with secrets.
6. **Infrastructure**: resources exposed publicly, wildcard IAM, unencrypted storage/transit,
   containers running privileged or as root, missing network restrictions.

For each finding state the attacker-controlled input and the sink in `explanation`. If you cannot
name both from the visible diff, lower the confidence or skip it. Skip patterns the deterministic
rules already catch (listed under "Already reported").
