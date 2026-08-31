# Licensing Security Boundary

Production private signing material is external to this repository.

The normal product verifies signed licenses with public verification material only. The offline License Generator loads private signing material only from an explicit controlled external source and fails closed when it is not supplied or is invalid. Tests may create ephemeral keys at runtime; those keys are not production credentials.
