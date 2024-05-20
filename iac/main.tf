resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${local.name}-logs"
  retention_in_days = 7
}

resource "aws_acm_certificate" "this" {
  provider = aws.primary_region

  domain_name       = local.application_url
  validation_method = "DNS"

  tags = {
    Name = "${local.name}"
  }
}

resource "aws_route53_record" "validation" {
  for_each = {
    for dvo in aws_acm_certificate.this.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = data.aws_route53_zone.this.zone_id
}

resource "aws_acm_certificate_validation" "this" {
  certificate_arn         = aws_acm_certificate.this.arn
  validation_record_fqdns = [for record in aws_route53_record.validation : record.fqdn]
}

resource "aws_iam_role" "ecs-task-role" {
  name = "${local.name}-ecs-task-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      },
    ]
  })
}

resource "aws_iam_policy" "ecs-task-policy" {
  name = "${local.name}-ecs-task-policy"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "dynamodb:DescribeTable",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem"
        ]
        Resource = "${aws_dynamodb_table.asset.arn}"
      },
      {
        Effect = "Allow"
        Action = [
            "logs:CreateLogStream",
            "logs:PutLogEvents"
        ],
        Resource = "${aws_cloudwatch_log_group.this.arn}:*"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "ecs-task-policy-attachment" {
  role       = aws_iam_role.ecs-task-role.name
  policy_arn = aws_iam_policy.ecs-task-policy.arn
}

resource "aws_iam_role" "ecs-task-execution-role" {
  name = "${local.name}-ecs-task-execution-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      },
    ]
  })
}

resource "aws_iam_role_policy_attachment" "ecs-task-execution-role" {
  role       = aws_iam_role.ecs-task-execution-role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_security_group" "alb" {
  provider = aws.primary_region

  name        = "alb"
  description = "Security group for ALB"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "ecs" {
  provider = aws.primary_region

  name        = "${local.name}-ecs"
  description = "Security group for ECS tasks"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = 443
    to_port         = 443
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_lb" "this" {
  provider = aws.primary_region

  name               = local.name
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = [aws_subnet.public[0].id, aws_subnet.public[1].id]
}

resource "aws_lb_target_group" "this" {
  provider = aws.primary_region

  name        = local.name
  port        = 443
  protocol    = "HTTPS"
  protocol_version = "HTTP2"
  target_type = "ip"
  vpc_id      = aws_vpc.main.id

  health_check {
    path                = "/"
    protocol            = "HTTPS"
    healthy_threshold   = 2
    unhealthy_threshold = 2
    timeout             = 5
    interval            = 30
    matcher             = "200"
  }
}

resource "aws_lb_listener" "this" {
  provider = aws.primary_region

  depends_on = [ aws_acm_certificate_validation.this ]

  load_balancer_arn = aws_lb.this.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate.this.arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.this.arn
  }
}

resource "aws_ecs_cluster" "this" {
  provider = aws.primary_region

  name = local.name
}

resource "aws_ecs_task_definition" "this" {
  provider = aws.primary_region

  family                   = local.name
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  execution_role_arn       = aws_iam_role.ecs-task-execution-role.arn
  task_role_arn = aws_iam_role.ecs-task-role.arn
  container_definitions = jsonencode([
    {
      name      = "${local.name}",
      image     = "${data.aws_ecr_image.this.image_uri}",
      essential = true,
      environment = [
        {
          "name" : "ASPNETCORE_ENVIRONMENT",
          "value" : "${local.environment}"
        },
        {
            "name": "DYNAMODB_SERVICE_URL",
            "value": "https://dynamodb.${local.primary_region}.amazonaws.com"
        }

      ],
      portMappings = [
        {
          containerPort = 443,
          hostPort      = 443
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
            "awslogs-group"         = "/ecs/${local.name}-logs",
            "awslogs-region"        = "${local.primary_region}",
            "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
  cpu    = "256"
  memory = "512"
}

resource "aws_ecs_service" "this" {
  provider = aws.primary_region

  depends_on = [ 
    aws_lb.this, 
    aws_lb_listener.this, 
    aws_lb_target_group.this 
    ]

  name            = local.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.this.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = [aws_subnet.private[0].id, aws_subnet.private[1].id]
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.this.arn
    container_name   = local.name
    container_port   = 443
  }
}

resource "aws_dynamodb_table" "asset" {
  provider = aws.primary_region

  name           = "Asset"
  billing_mode   = "PROVISIONED"
  read_capacity  = 5
  write_capacity = 5

  hash_key = "Id"

  attribute {
    name = "Id"
    type = "S"
  }

  tags = {
    Name = "${local.name}-Asset"
  }
}

resource "aws_route53_record" "this" {
  zone_id = data.aws_route53_zone.this.zone_id
  name = "${local.application_url}"
  type = "A"

  alias {
    name = aws_lb.this.dns_name
    zone_id = aws_lb.this.zone_id
    evaluate_target_health = true
  }
}