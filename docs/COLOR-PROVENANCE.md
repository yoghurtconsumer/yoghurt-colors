# Color algorithm provenance

The saturation, affine controls, bounded tone curve and residual redistribution
correction are implemented independently from the mathematical definitions in
TECHNICAL-SPECIFICATION.md, not copied from another display utility.

CVD simulation numerical coefficients were checked on 15 October 2026 against
the authors' [research page and precomputed table](https://www.inf.ufrgs.br/~oliveira/pubs_files/CVD_Simulation/CVD_Simulation.html):
Machado, Oliveira and Fernandes, IEEE TVCG 15(6), 2009, pages 1291–1298.
DaltonizationBuilder records the severity-1 numerical results for the three
simulation types, transposed from the published column-vector convention.
Tests independently check orientation and the residual redistribution formula.

Only numerical research results are included. No author source code, executable,
paper, page HTML, image or other research asset is redistributed. The inspected
page does not grant a general software/asset redistribution licence; none is
assumed. The implementation of D = I + e*(I-C)*K is original source, distinct from
the research simulation C. Project licence selection and release review remain
open, as recorded in LICENSE and RELEASE-CHECKLIST.md.

The correction is experimental, strength-adjustable and off by default. It
operates on encoded SDR appearance values, omits true sRGB decode/encode, and
does not claim medical efficacy or perceptual equivalence. Affected-user and
hardware qualification is still required. Simulation is not correction.
