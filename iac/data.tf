data "aws_route53_zone" "this" {
  name = "dev.usemyapps.com"
}

data "aws_ecr_repository" "this" {
  name = local.name
}

data "aws_ecr_image" "this" {
  repository_name = data.aws_ecr_repository.this.name
  most_recent     = true
}
