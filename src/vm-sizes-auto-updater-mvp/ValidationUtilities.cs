using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Compute.Supportability.Tools
{
    /// <summary>
    /// Sets of utility functions to validate arguments.
    /// </summary>
    /// FROM GARTNER AVAIL
    public static class ValidationUtility
    {
        /// <summary>
        /// Validates that the string parameter is not whitespace, null, or empty.
        /// </summary>
        /// <param name="argument">The argument to validate.</param>
        /// <param name="parameterName">The name of the argument for the exception.</param>
        /// <exception cref="System.ArgumentException">Thrown when the argument is null, whitespace or empty.</exception>
        public static void EnsureIsNotNullOrWhiteSpace(in string argument, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(argument?.Trim())) {
                throw new ArgumentException($"Argument {parameterName} cannot be whitespace, null, or empty.");
            }
        }

        /// <summary>
        /// Validates that a given Guid argument is not empty.
        /// </summary>
        /// <param name="guidArg">The argument to validate.</param>
        /// <param name="parameterName">The name of the argument.</param>
        public static void EnsureGuidIsNotEmpty(in Guid guidArg, string parameterName)
        {
            if (guidArg == Guid.Empty) {
                throw new ArgumentException($"Guid Argument {parameterName} cannot be empty.");
            }
        }

        /// <summary>
        /// Validates that the object is not null.
        /// </summary>
        /// <param name="argument">The argument to validate.</param>
        /// <param name="parameterName">The name of the argument for the exception.</param>
        /// <exception cref="System.ArgumentException">Thrown when the argument is null.</exception>
        public static void EnsureIsNotNull(in object argument, string parameterName)
        {
            if (argument == null) {
                throw new ArgumentException($"Argument {parameterName} cannot be null.");
            }
        }

        /// <summary>
        /// Validates that the argument is not zero.
        /// </summary>
        /// <param name="argument">The argument to validate.</param>
        /// <param name="parameterName">The name of the argument for the exception.</param>
        /// <exception cref="System.ArgumentException">Thrown when the argument is zero.</exception>
        public static void EnsureIsNotZero(in uint argument, string parameterName)
        {
            EnsureIsNotNull(argument, parameterName);
            if (argument == 0) {
                throw new ArgumentException($"Argument {parameterName} must be a non-positive integer.");
            }
        }

        /// <summary>
        /// Validates that the argument is positive.
        /// </summary>
        /// <param name="argument">The argument to validate.</param>
        /// <param name="parameterName">The name of the argument for the exception.</param>
        /// <exception cref="System.ArgumentException">Thrown when the argument is zero or negative.</exception>
        public static void EnsureIsPositive(in double argument, string parameterName)
        {
            EnsureIsNotNull(argument, parameterName);
            if (argument <= 0) {
                throw new ArgumentException($"Argument {parameterName} must be a positive.");
            }
        }

        /// <summary>
        /// Validates that the collection argument is not null and not empty.
        /// </summary>
        /// <param name="argument">The collection argument to validate.</param>
        /// <param name="parameterName">The name of the argument for the exception.</param>
        /// <exception cref="System.ArgumentException">Thrown when the argument is zero or negative.</exception>
        public static void EnsureIsNotEmpty<T>(in ICollection<T> argument, string parameterName)
        {
            EnsureIsNotNull(argument, parameterName);
            if (argument.Count <= 0) {
                throw new ArgumentException($"Argument {parameterName} must not be empty.");
            }
        }
    }
}
